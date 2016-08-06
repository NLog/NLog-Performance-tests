@echo off

set folder=%2
set trimmedFolder=%folder:*\=%
echo %1\NLogPerformance.exe 100000 32 1 > Results\%3_%trimmedFolder%.txt
call %1\NLogPerformance.exe 100000 32 1 > Results\%3_%trimmedFolder%.txt
