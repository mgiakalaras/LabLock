@echo off
chcp 65001 >nul
setlocal EnableExtensions
title LabLock - ανέβασμα στο GitHub

rem Ανεβάζει το LabLock στο https://github.com/mgiakalaras/LabLock με ένα διπλό κλικ.
rem Την πρώτη φορά φτιάχνεις το αποθετήριο στο GitHub (ανοίγει μόνο του) και συνδέεσαι από τον browser.
rem Σε κάθε νέα έκδοση (AssemblyVersion στο LabLock\Properties\AssemblyInfo.cs) το GitHub
rem φτιάχνει μόνο του Release με έτοιμο το LabLock.exe.

set "OWNER=mgiakalaras"
set "NAME=LabLock"
set "REPO=https://github.com/%OWNER%/%NAME%.git"
set "HERE=%~dp0"
set "WORK=%TEMP%\LabLock-github"

where git >nul 2>&1
if errorlevel 1 goto nogit

rem ---- η έκδοση, από το AssemblyInfo.cs (0.4.2.0 -> 0.4.2)
set "VER="
for /f "tokens=2 delims=()" %%v in ('findstr /c:"AssemblyVersion(" "%HERE%LabLock\Properties\AssemblyInfo.cs"') do set "VER=%%~v"
if not defined VER goto noversion
for /f "tokens=1-3 delims=." %%a in ("%VER%") do set "VER=%%a.%%b.%%c"
echo.
echo  LabLock %VER%  -^>  %REPO%
echo.

rem ---- υπάρχει το αποθετήριο;
git ls-remote "%REPO%" >nul 2>&1
if not errorlevel 1 goto haverepo
echo  Το αποθετήριο δεν υπάρχει ακόμη στο GitHub.
echo  Ανοίγει τώρα η σελίδα δημιουργίας: όνομα LabLock, Public, και ΧΩΡΙΣ README / .gitignore / license.
echo  Πάτα "Create repository" και μετά γύρνα εδώ.
start "" "https://github.com/new?name=%NAME%&visibility=public&description=Lockdown+tool+for+school+computer+labs+(Windows+10%%2F11)"
echo.
pause
git ls-remote "%REPO%" >nul 2>&1
if errorlevel 1 goto norepo

:haverepo
rem ---- αντίγραφο του αποθετηρίου σε προσωρινό φάκελο, και από πάνω η νέα έκδοση
if exist "%WORK%" rmdir /s /q "%WORK%"
git clone --quiet "%REPO%" "%WORK%"
if errorlevel 1 goto failed

robocopy "%HERE%." "%WORK%" /MIR /XD .git .github bin obj .vs /XF LabLock.exe build-log.txt LabLock.ini LabLock-log.txt LabLock-computers.csv LabLock-veyon.csv LabLock-nethawk-machines.json LabLock-blocklist.txt *.zip /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 goto failed

rem ---- το GitHub Action (χτίζει το LabLock.exe): από το ci\build.yml στη θέση που το θέλει το GitHub
if not exist "%WORK%\.github\workflows" mkdir "%WORK%\.github\workflows"
copy /y "%HERE%ci\build.yml" "%WORK%\.github\workflows\build.yml" >nul

pushd "%WORK%"
git config user.name >nul 2>&1 || git config user.name "%OWNER%"
git config user.email >nul 2>&1 || git config user.email "%OWNER%@users.noreply.github.com"
git add -A
git diff --cached --quiet
if errorlevel 1 (
    git commit --quiet -m "LabLock %VER%"
    echo  Οι αλλαγές καταχωρήθηκαν.
) else (
    echo  Καμία αλλαγή από το τελευταίο ανέβασμα.
)
git branch -M main
git push --quiet -u origin main
if errorlevel 1 goto failedpop

rem ---- νέα έκδοση: το GitHub Action φτιάχνει μόνο του το Release vX.Y.Z με το LabLock.exe
git ls-remote --tags origin "refs/tags/v%VER%" | findstr /c:"v%VER%" >nul
if not errorlevel 1 goto oldversion
echo.
echo  Νέα έκδοση v%VER%. Σε 2-3 λεπτά το LabLock.exe θα είναι στη σελίδα Releases:
echo  https://github.com/%OWNER%/%NAME%/releases
goto done

:oldversion
echo.
echo  Η έκδοση v%VER% υπάρχει ήδη: ανέβηκαν μόνο οι αλλαγές, χωρίς νέο Release.
echo  Για νέο Release άλλαξε πρώτα την έκδοση στο LabLock\Properties\AssemblyInfo.cs.

:done
popd
echo.
echo  Έλεγχος μεταγλώττισης: https://github.com/%OWNER%/%NAME%/actions  (πράσινο = εντάξει)
echo.
echo  Έτοιμο!
pause
exit /b 0

:nogit
echo  Δεν βρέθηκε το Git. Εγκατάστησέ το από https://git-scm.com/download/win
echo  ή από PowerShell:  winget install Git.Git   και ξανατρέξε αυτό το αρχείο.
pause
exit /b 1

:noversion
echo  Δεν βρέθηκε η έκδοση στο LabLock\Properties\AssemblyInfo.cs.
pause
exit /b 1

:norepo
echo  Το αποθετήριο %REPO% ακόμη δεν βρίσκεται. Φτιάξ' το στο GitHub και ξανατρέξε αυτό το αρχείο.
pause
exit /b 1

:failedpop
popd
:failed
echo.
echo  Κάτι απέτυχε (δες τα μηνύματα πιο πάνω). Τίποτα δεν χάθηκε από τον φάκελό σου.
pause
exit /b 1
