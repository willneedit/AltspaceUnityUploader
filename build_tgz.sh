#!/bin/bash

pkgname="org.willneedit.altspace_unity_uploader"
dllfile="Library/ScriptAssemblies/${pkgname}.dll"
tgtdir=$(pwd)

if [ "x$1" != "x" ]; then
	tgtdir="$1"
fi

cd $(dirname "$0")

echo ${tgtdir}/${pkgname}.tgz

if [ ! -f ../../${dllfile} ]; then
	echo "Need compiled DLL of plugin - simply have Unity run."
	exit 1
fi

rm -rf ../package
cp -lr . ../package
cd ../package

mkdir Plugins
cp ../../${dllfile} Plugins

base64 -d <<EOF >Plugins.meta
ZmlsZUZvcm1hdFZlcnNpb246IDIKZ3VpZDogNGNmOGZiYTk1YWYzMjZhNDFiN2E1M2Y2YWMzMDky
Y2MKZm9sZGVyQXNzZXQ6IHllcwpEZWZhdWx0SW1wb3J0ZXI6CiAgZXh0ZXJuYWxPYmplY3RzOiB7
fQogIHVzZXJEYXRhOiAKICBhc3NldEJ1bmRsZU5hbWU6IAogIGFzc2V0QnVuZGxlVmFyaWFudDog
Cg==
EOF

base64 -d <<EOF >Plugins/${pkgname}.dll.meta
ZmlsZUZvcm1hdFZlcnNpb246IDIKZ3VpZDogNzYwZDk2ZTdlOWNjMTIzNGJhODMxOTQ4ZGZkMDM2
MzkKUGx1Z2luSW1wb3J0ZXI6CiAgZXh0ZXJuYWxPYmplY3RzOiB7fQogIHNlcmlhbGl6ZWRWZXJz
aW9uOiAyCiAgaWNvbk1hcDoge30KICBleGVjdXRpb25PcmRlcjoge30KICBkZWZpbmVDb25zdHJh
aW50czogW10KICBpc1ByZWxvYWRlZDogMAogIGlzT3ZlcnJpZGFibGU6IDAKICBpc0V4cGxpY2l0
bHlSZWZlcmVuY2VkOiAwCiAgdmFsaWRhdGVSZWZlcmVuY2VzOiAxCiAgcGxhdGZvcm1EYXRhOgog
IC0gZmlyc3Q6CiAgICAgIEFueTogCiAgICBzZWNvbmQ6CiAgICAgIGVuYWJsZWQ6IDEKICAgICAg
c2V0dGluZ3M6IHt9CiAgLSBmaXJzdDoKICAgICAgRWRpdG9yOiBFZGl0b3IKICAgIHNlY29uZDoK
ICAgICAgZW5hYmxlZDogMAogICAgICBzZXR0aW5nczoKICAgICAgICBEZWZhdWx0VmFsdWVJbml0
aWFsaXplZDogdHJ1ZQogIC0gZmlyc3Q6CiAgICAgIFdpbmRvd3MgU3RvcmUgQXBwczogV2luZG93
c1N0b3JlQXBwcwogICAgc2Vjb25kOgogICAgICBlbmFibGVkOiAwCiAgICAgIHNldHRpbmdzOgog
ICAgICAgIENQVTogQW55Q1BVCiAgdXNlckRhdGE6IAogIGFzc2V0QnVuZGxlTmFtZTogCiAgYXNz
ZXRCdW5kbGVWYXJpYW50OiAK
EOF

rm -rf Editor Editor.meta

rm -rf build_tgz.sh build_tgz.sh.meta .git

cd ..
tar -czf ${tgtdir}/${pkgname}.tgz package
rm -rf package
