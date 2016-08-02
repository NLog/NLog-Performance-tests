@echo off

set folder=%1
set trimmed=%folder:*\=%

for /D %%A IN ("Config\*") DO (
  copy %%A\nlog.config %1 /Y
  timeout /t 5 /nobreak
  call runTestTemplate1.bat %1 %%A %trimmed%
)

mkdir logs\%trimmed%
xcopy C:\logs\application\* logs\%trimmed%\* /s /y