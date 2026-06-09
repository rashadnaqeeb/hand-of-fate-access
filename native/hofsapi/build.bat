@echo on
REM Builds the x86 HofSapi.dll SAPI shim. The VS path matches third_party\tolk\build_x86.bat.
REM Run once when hofsapi.cpp changes; the resulting HofSapi.dll is vendored beside this file
REM and build.ps1 deploys it like the Tolk runtime.
call "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvarsall.bat" x86
cd /d "%~dp0"
cl /nologo /O2 /EHsc /LD /W4 /DUNICODE /D_UNICODE /Fe:HofSapi.dll hofsapi.cpp Ole32.lib
echo BUILD_EXIT=%errorlevel%
