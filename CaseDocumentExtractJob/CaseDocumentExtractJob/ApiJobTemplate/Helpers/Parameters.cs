using System.Xml;
using Tyler.Odyssey.Utils;

namespace CaseDocumentExtractJob.Helpers
{
    public class Parameters
    {
        public string CaseNumber { get; private set; }
        public string NodeID { get; private set; }
        public string Location { get; private set; }
        public string BaseDocumentPath { get; private set; }
        public string ReportFolderName { get; private set; }
        public string InputFolderName { get; private set; }

        public Parameters(XmlElement taskNode, UtilsLogger logger)
        {
            logger.WriteToLog("Beginning Parameters() constructor", LogLevel.Verbose);
            logger.WriteToLog("taskNode: " + taskNode.OuterXml, LogLevel.Verbose);

            Location = taskNode.GetAttribute("Location");
            NodeID = taskNode.GetAttribute("NodeID");
            CaseNumber = taskNode.GetAttribute("CaseNumber");
            
            BaseDocumentPath = taskNode.GetAttribute("BaseDocumentPath");            
            ReportFolderName = taskNode.GetAttribute("ReportFolderName");
            InputFolderName = taskNode.GetAttribute("InputFolderName");

            logger.WriteToLog("Instantiated Parameters", LogLevel.Verbose);
        }
    }
}