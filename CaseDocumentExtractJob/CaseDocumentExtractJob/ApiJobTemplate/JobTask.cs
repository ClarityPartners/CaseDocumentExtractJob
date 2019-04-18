using System;
using System.Runtime.InteropServices;
using Tyler.Odyssey.JobProcessing;

namespace CaseDocumentExtractJob
{
  [ClassInterface(ClassInterfaceType.None)]
  [Guid("dc83daf0-8eb7-4a2b-a034-49816f27c140")]
  [ComVisible(true)]
  public class JobTask : Task
  {
    protected override void SetupProcessor(string SiteID, string JobTaskXML)
    {
      Processor = new DataProcessor(SiteID, JobTaskXML);

      ((DataProcessor)Processor).TaskParms = this.jobTaskParms;
      ((DataProcessor)Processor).TaskUtility = this.taskUtility;
      ((DataProcessor)Processor).TaskDocument = this.taskDocument;

      UserID = ((DataProcessor)Processor).Context.UserID;
    }

    private int UserID { get; set; }
  }
}
