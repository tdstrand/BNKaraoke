@echo off
cd C:\Users\tstra\source\repos\BNKaraoke\bnkaraoke.web
cls
echo Removing Cache Files
rmdir /s /q build
call npm cache clean --force >nul 2>&1
echo Running Build
call npm run build
echo Starting FrontEnd
call npx serve -s build -l 8080