@echo off
echo ========================================================
echo   Microservices Task Manager - Startup Launcher
echo ========================================================
echo.
cd /d "%~dp0"

echo [1/3] Verifying Docker environment...
docker --version >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Docker is not running or not found in PATH!
    echo Please ensure Docker Desktop is started.
    pause
    exit /b 1
)

echo [2/3] Building and starting all microservices with Docker Compose...
echo Running: docker compose up -d --build
docker compose up -d --build

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================================
    echo   ALL MICROSERVICES ARE RUNNING!
    echo ========================================================
    echo   Web Dashboard:        http://localhost:3000
    echo   UserService API:      http://localhost:5001/swagger
    echo   TaskService API:      http://localhost:5002/swagger
    echo   NotificationService:  http://localhost:5003/swagger
    echo ========================================================
    echo.
    echo Opening Web Dashboard in browser...
    start http://localhost:3000
) else (
    echo.
    echo [ERROR] Docker Compose build/startup failed.
)

pause