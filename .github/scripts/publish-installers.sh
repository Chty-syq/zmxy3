#!/usr/bin/env bash
set -euo pipefail

version=${RELEASE_TAG#v}
windows="ZmxyStudio-${version}-Windows-x64-Setup.exe"
macos="ZmxyStudio-${version}-macOS-universal.dmg"
cd dist
for installer in "$windows" "$macos"; do
  test -s "$installer"
done
sha256sum "$windows" "$macos" > SHA256SUMS.txt
assets=("$windows" "$macos" SHA256SUMS.txt)

# A branch can advance while the installers are being tested.
commit=$(git rev-parse HEAD)
git fetch --quiet --no-tags --depth=1 origin "$GITHUB_REF"
if [[ $(git rev-parse 'FETCH_HEAD^{commit}') != "$commit" ]]; then
  echo "::error::The release ref changed during this build."
  exit 1
fi
tag_ref="refs/tags/$RELEASE_TAG"
if [[ -n $(git ls-remote --refs origin "$tag_ref") ]]; then
  git fetch --quiet --no-tags --depth=1 origin "$tag_ref"
  if [[ $(git rev-parse 'FETCH_HEAD^{commit}') != "$commit" ]]; then
    echo "::error::$RELEASE_TAG already points to another commit. Choose a new version."
    exit 1
  fi
else
  git push origin "$commit:$tag_ref"
fi

if ! release=$(gh release view "$RELEASE_TAG" --json databaseId,isDraft 2>/dev/null); then
  gh release create "$RELEASE_TAG" --verify-tag --target "$commit" --draft \
    --title "造梦工坊 $version" --notes-file - <<'NOTES'
Windows 10/11 x64 请下载 `.exe`，安装后从开始菜单打开。游戏需要 .NET Framework 4.8、Visual C++ x64 运行库和官方 Flash PPAPI 64 位组件。

macOS 13 及以上请下载 `.dmg`，打开镜像，将「造梦工坊」拖入「应用程序」。一个安装包同时支持 Apple 芯片和 Intel Mac，内置 Ruffle 播放器。

此版本的 Windows 安装包未做 Authenticode 签名，Mac 应用使用临时签名，未经 Apple 公证。可用附件 `SHA256SUMS.txt` 核对下载文件。
NOTES
  release=$(gh release view "$RELEASE_TAG" --json databaseId,isDraft)
fi
release_id=$(jq -er '.databaseId' <<< "$release")
assets_url="repos/$GH_REPO/releases/$release_id/assets"
if [[ $(jq -r '.isDraft' <<< "$release") == false ]]; then
  # A rerun can produce new binary hashes. Keep the published assets intact.
  uploaded=$(gh api "$assets_url")
  for file in "${assets[@]}"; do
    jq -e --arg name "$file" 'any(.[]; .name == $name and .state == "uploaded" and .size > 0)' <<< "$uploaded" >/dev/null
  done
  echo "$RELEASE_TAG has already been published."
  exit 0
fi

gh release upload "$RELEASE_TAG" "${assets[@]}" --clobber
uploaded=$(gh api "$assets_url")
for file in "${assets[@]}"; do
  digest=$(sha256sum "$file" | cut -d' ' -f1)
  if ! jq -e --arg name "$file" --arg digest "sha256:$digest" \
        'any(.[]; .name == $name and .state == "uploaded" and .digest == $digest)' <<< "$uploaded" >/dev/null; then
    echo "::error::GitHub did not confirm the SHA-256 checksum for $file."
    exit 1
  fi
done
gh release edit "$RELEASE_TAG" --draft=false
