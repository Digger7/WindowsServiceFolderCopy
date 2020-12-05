@ECHO OFF

net stop FolderCopyService
sc delete FolderCopyService

pause
echo Done.