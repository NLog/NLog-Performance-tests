@echo off
mkdir logs
mkdir results

for /D %%A IN ("Bin\*") DO (
  del /S /Q C:\logs\application\*
  call runTestTemplate.bat %%A
)