@echo off
echo ========================================================
echo   Pushing Fixed Project to GitHub: microservices-taskmanager
echo ========================================================
echo.
cd /d "%~dp0"

echo Current Git Status:
git status
echo.

echo Pushing fix to branch main on GitHub...
git push origin main

echo.
if %ERRORLEVEL% EQU 0 (
    echo ========================================================
    echo   SUCCESS: Fix has been pushed to GitHub!
    echo   https://github.com/divyakumari1107/microservices-taskmanager
    echo ========================================================
) else (
    echo ========================================================
    echo   Push failed. Check your Git credentials or network.
    echo ========================================================
)

pause