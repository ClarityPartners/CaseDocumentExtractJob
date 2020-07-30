using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CaseDocumentExtractJob.Helpers
{
  public class Constants
  {
    // TODO: Change the site name to match the site you're working on.
    public const string SiteName = "ILCOOKTEST2";

    // TODO:  Set the logging registry key to the name of your job.
    // The registry key provided here is what the job will look for in the following registry location:
    //     HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\The Software Group\Meridian\Job Processing
    // to determine whether logging should be enabled during the job run.
    // The key must have three values:  HistToKeep, LogLevel, and LogPath.
    // HistToKeep:  Number of history items to keep in the history.  10 is recommended.
    // LogLevel:  1 - Basic, 2 - Intermediate, 3 - Verbose.  3 is recommended.
    // LogPath:  The path to which log files will be saved on the server.  "C:\Logs\<JobName>" is recommended.
    internal const string LOG_REGISTRY_KEY = "CaseDocumentExtractJob";
    public const string systemUserID = "1";
  }
}
