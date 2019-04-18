using System.Xml;
using Tyler.Odyssey.Utils;

namespace CaseDocumentExtractJob.Helpers
{
  public class Parameters
  {
    public string CaseNumber { get; private set; }
    public string NodeID { get; private set; }
    public string DocumentPath { get; private set; }

    public Parameters(XmlElement taskNode, UtilsLogger logger)
    {
      logger.WriteToLog("Beginning Parameters() constructor", LogLevel.Verbose);
      logger.WriteToLog("taskNode: " + taskNode.OuterXml, LogLevel.Verbose);

      CaseNumber = taskNode.GetAttribute("CaseNumber");
      NodeID = taskNode.GetAttribute("NodeID");
      DocumentPath = taskNode.GetAttribute("DocumentPath");

      logger.WriteToLog("Instantiated Parameters", LogLevel.Verbose);
    }
  }
}