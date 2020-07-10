@ECHO OFF

net stop GuardService
sc delete GuardService

pause
echo Done.