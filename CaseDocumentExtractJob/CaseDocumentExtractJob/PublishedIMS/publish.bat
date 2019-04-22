@echo off
setlocal enableextensions
set prompt= $g 



:: *****************************************************************************************
:: When using this PUBLISH.BAT for a new feature, you MUST update the following definitions!
:: *****************************************************************************************

set ProjectNumber="12753"
set ConsoleLabel="CaseDocumentExportJob"
set ConsoleGroup="Integration/Job Processing"



:: *******************************************
:: Do NOT alter the commands below this line!
:: ******************************************

set FeatureName="%ProjectNumber%"
set OutputFile="%ProjectNumber%.zip"

if "%IMSFeatureIncrement%" == "" set IMSFeatureIncrement="0.0.0.1"

if "%LinkISMode%" == "" set LinkISMode = COPYPRIOR
:: ** Commented Out the GetISVersion Increment as this will be handled by Build server
::for /f "tokens=*" %%A in ('GetISVersion %FeatureName% /Increment %IMSFeatureIncrement%') do set Version=%%A

if "%DoPromote%"=="1" set PromoteCommand=PromoteIS "%ProjectNumber%,Type=Feature,Version=%Version%"
if "%DoLink%"=="1"    set LinkCommand=LinkIS "%ProjectNumber%,Version=%Version%" /Mode %LinkISMode% /Customers "%LinkParameters%"

cd

@echo on

BuildIS /Feature %FeatureName% /Version %Version% /Label %ConsoleLabel% /Output %OutputFile% /InstallCat Ext .

PublishIS %OutputFile% /Group %ConsoleGroup%

::del %OutputFile%

%PromoteCommand%

%LinkCommand%

if exist %TylerAccurevAutomationToolsPath%\IMSFeaturePostPublishBat.ps1 (
  powershell.exe %TylerAccurevAutomationToolsPath%\IMSFeaturePostPublishBat.ps1 '%ProjectNumber%' '%ConsoleLabel%' '%ConsoleGroup%' %Version% %IMSFeatureIncrement% %DoPromote% %TylerAccurevAutomationToolsPath% %1
)
