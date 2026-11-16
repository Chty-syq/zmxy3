#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <mmsystem.h>
#include <stdint.h>
#include "MinHook.h"

/* Default 1x installs no hooks. A monitor, never the clock hot path, handles
   shared-memory reads and the watchdog. Published clock states are immutable. */
typedef struct { DWORD magic; volatile LONG speed_milli; volatile LONG64 heartbeat;
 volatile LONG ready; DWORD pid; volatile LONG hooks_active; } Control;
typedef struct ClockState { double rate; LARGE_INTEGER anchor; DWORD time,tick;
 ULONGLONG tick64; LONGLONG offset64; DWORD offset_time,offset_tick; struct ClockState *previous; } ClockState;
static Control *control;
static HANDLE mapping,stop_event,monitor;
static ClockState * volatile clock_state;
static LARGE_INTEGER frequency;
static DWORD (WINAPI *real_time)(void);
static DWORD (WINAPI *real_tick)(void);
static ULONGLONG (WINAPI *real_tick64)(void);
static BOOL started,hooks_active;
__declspec(dllexport) ULONGLONG __cdecl ZmxyRealTime(void){return real_tick64?real_tick64():GetTickCount64();}
static ULONGLONG scaled_delta(ClockState *s){LARGE_INTEGER now;QueryPerformanceCounter(&now);return (ULONGLONG)((now.QuadPart-s->anchor.QuadPart)*1000.0*s->rate/frequency.QuadPart);}
static ClockState *current(void){ClockState *s=clock_state;MemoryBarrier();return s;}
static DWORD WINAPI hooked_time(void){ClockState *s=current();return s->rate==1.0?real_time()+s->offset_time:s->time+(DWORD)scaled_delta(s);}
static DWORD WINAPI hooked_tick(void){ClockState *s=current();return s->rate==1.0?real_tick()+s->offset_tick:s->tick+(DWORD)scaled_delta(s);}
static ULONGLONG WINAPI hooked_tick64(void){ClockState *s=current();return s->rate==1.0?real_tick64()+s->offset64:s->tick64+scaled_delta(s);}
static void change_rate(double rate){
 ClockState *old=current();if(old&&old->rate==rate)return;
 ClockState *s=HeapAlloc(GetProcessHeap(),HEAP_ZERO_MEMORY,sizeof(ClockState));if(!s)return;
 DWORD now_time=real_time(),now_tick=real_tick();ULONGLONG now64=real_tick64();
 QueryPerformanceCounter(&s->anchor);s->rate=rate;s->previous=old;
 if(!old){s->time=now_time;s->tick=now_tick;s->tick64=now64;}
 else if(old->rate==1.0){s->time=now_time+old->offset_time;s->tick=now_tick+old->offset_tick;s->tick64=now64+old->offset64;}
 else{ULONGLONG delta=(ULONGLONG)((s->anchor.QuadPart-old->anchor.QuadPart)*1000.0*old->rate/frequency.QuadPart);s->time=old->time+(DWORD)delta;s->tick=old->tick+(DWORD)delta;s->tick64=old->tick64+delta;}
 s->offset64=(LONGLONG)(s->tick64-now64);s->offset_time=s->time-now_time;s->offset_tick=s->tick-now_tick;
 InterlockedExchangePointer((PVOID volatile*)&clock_state,s);
 if(!hooks_active&&rate!=1.0&&MH_EnableHook(MH_ALL_HOOKS)==MH_OK){hooks_active=TRUE;InterlockedExchange(&control->hooks_active,1);}
}
static DWORD WINAPI watch_control(LPVOID unused){
 do{LONG milli=InterlockedCompareExchange(&control->speed_milli,0,0);
 ULONGLONG heartbeat=(ULONGLONG)InterlockedCompareExchange64(&control->heartbeat,0,0),now=real_tick64();
 change_rate(((milli==1000||milli==2000||milli==3000||milli==100000)&&now>=heartbeat&&now-heartbeat<4000)?milli/1000.0:1.0);
 }while(WaitForSingleObject(stop_event,10)==WAIT_TIMEOUT);return 0;
}
__declspec(dllexport) void __cdecl ZmxyShutdown(void);
__declspec(dllexport) int __cdecl ZmxyInitialize(void){
 if(started)return 1;WCHAR channel[160];
 if(!GetEnvironmentVariableW(L"ZMXY_SPEED_CHANNEL",channel,160))return -1;
 mapping=OpenFileMappingW(FILE_MAP_READ|FILE_MAP_WRITE,FALSE,channel);if(!mapping)return -2;
 control=(Control*)MapViewOfFile(mapping,FILE_MAP_READ|FILE_MAP_WRITE,0,0,64);
 if(!control||control->magic!=0x5A4D5833){ZmxyShutdown();return -3;}
 LoadLibraryW(L"winmm.dll");if(MH_Initialize()!=MH_OK){ZmxyShutdown();return -4;}
 if(MH_CreateHookApi(L"winmm.dll","timeGetTime",hooked_time,(LPVOID*)&real_time)!=MH_OK||
 MH_CreateHookApi(L"kernel32.dll","GetTickCount",hooked_tick,(LPVOID*)&real_tick)!=MH_OK||
 MH_CreateHookApi(L"kernel32.dll","GetTickCount64",hooked_tick64,(LPVOID*)&real_tick64)!=MH_OK){MH_Uninitialize();ZmxyShutdown();return -5;}
 QueryPerformanceFrequency(&frequency);change_rate(1.0);
 if(!clock_state){MH_Uninitialize();ZmxyShutdown();return -6;}
 stop_event=CreateEventW(NULL,TRUE,FALSE,NULL);if(!stop_event){MH_Uninitialize();ZmxyShutdown();return -7;}
 started=TRUE;control->pid=GetCurrentProcessId();InterlockedIncrement(&control->ready);
 monitor=CreateThread(NULL,0,watch_control,NULL,0,NULL);if(!monitor){ZmxyShutdown();return -8;}return 1;
}
__declspec(dllexport) void __cdecl ZmxyShutdown(void){
 if(stop_event)SetEvent(stop_event);
 if(monitor){WaitForSingleObject(monitor,INFINITE);CloseHandle(monitor);monitor=NULL;}
 if(started){MH_DisableHook(MH_ALL_HOOKS);MH_Uninitialize();InterlockedDecrement(&control->ready);InterlockedExchange(&control->hooks_active,0);started=FALSE;}
 hooks_active=FALSE;if(stop_event){CloseHandle(stop_event);stop_event=NULL;}
 ClockState *s=clock_state;clock_state=NULL;while(s){ClockState *p=s->previous;HeapFree(GetProcessHeap(),0,s);s=p;}
 if(control){UnmapViewOfFile(control);control=NULL;}if(mapping){CloseHandle(mapping);mapping=NULL;}
 real_time=NULL;real_tick=NULL;real_tick64=NULL;
}
BOOL WINAPI DllMain(HINSTANCE instance,DWORD reason,LPVOID reserved){return TRUE;}
