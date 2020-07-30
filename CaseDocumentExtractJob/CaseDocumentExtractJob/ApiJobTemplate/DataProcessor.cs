using JobProcessingInterface;
using MSXML3;
using System;
using System.IO;
using Tyler.Odyssey.JobProcessing;
using Tyler.Odyssey.Utils;
using CaseDocumentExtractJob.Helpers;
using System.Linq;
using CaseDocumentExtractJob.Exceptions;
using System.Xml;
using System.Xml.Linq;
using System.Runtime.Serialization.Json;
using Tyler.Odyssey.API.JobTemplate;
using Tyler.Integration.Framework;
using System.Xml.Serialization;
using Tyler.Odyssey.API.Shared;
using Tyler.Integration.General;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;

namespace CaseDocumentExtractJob
{
    internal class DataProcessor : TaskProcessor
    {
        // Constructor
        public DataProcessor(string SiteID, string JobTaskXML) : base(SiteID, JobTaskXML)
        {
            Logger.WriteToLog("JobTaskXML:\r\n" + JobTaskXML, LogLevel.Basic);

            // New up the context object
            Context = new Context(Logger);

            Logger.WriteToLog("Completed instantiation of context object", LogLevel.Verbose);

            // Retrieve the parameters for the job (which flags to add/remove)
            Context.DeriveParametersFromJobTaskXML(SiteID, JobTaskXML);
            Context.ValidateParameters();

            Logger.WriteToLog("Finished deriving parameters", LogLevel.Verbose);

            // TODO:  Add the code tables that need to be updated to the following function (Context.AddCacheItems())
            Context.AddCacheItems();
            Context.UpdateCache();

            Logger.WriteToLog("Completed cache update.", LogLevel.Verbose);
        }

        // Static constructor
        static DataProcessor()
        {
            Logger = new UtilsLogger(LogManager);
            Logger.WriteToLog("Logger Instantiated", LogLevel.Basic);
        }

        // Destructor
        ~DataProcessor()
        {
            Logger.WriteToLog("Disposing!", LogLevel.Basic);

            if (Context != null)
                Context.Dispose();
        }

        public static IUtilsLogManager LogManager = new UtilsLogManagerBase(Constants.LOG_REGISTRY_KEY);
        public static readonly UtilsLogger Logger;

        public IXMLDOMDocument TaskDocument { get; set; }

        internal Context Context { get; set; }

        public ITYLJobTaskUtility TaskUtility { get; set; }

        private object taskParms;
        public object TaskParms { get { return taskParms; } set { taskParms = value; } }

        public override void Run()
        {
            Logger.WriteToLog("Beginning Run Method", LogLevel.Basic);
            string fullNodePath = "";
            // TODO: Update API Processing Logic
            try
            {
                string[] location;
                if (!string.IsNullOrEmpty(Context.Parameters.Location))
                    location = Context.Parameters.Location.Split(',');
                else
                {
                    string[] defaultLocation = { "" };
                    location = defaultLocation;
                }

                for (int i = 0; i < location.Length; i++)
                {
                    // Find all Documents Tied to the Case
                    string transactionResponse = ProcessCaseDocumentIDSearchTransaction(location[i]);
                    if (transactionResponse == null)
                    {
                        WriteToCaseManifest(Context.Parameters.CaseNumber
                            + ",,,,,,There was an error with the job.  Please speak to a system admin to view the error log.", fullNodePath);
                    }
                    else
                    {
                        // Extract documentIDs from response xml
                        string nodeID = ExtractNodeID(transactionResponse);
                        string caseType = ExtractCaseType(transactionResponse);
                        fullNodePath = GetFilePath(nodeID, caseType);
                        Logger.WriteToLog("Full Node Path: " + fullNodePath, LogLevel.Verbose);
                    }

                    // Extract data points from document response xml
                    List<Entities.FileNameInfo> caseDocumentIDList = ExtractTransactionElements(transactionResponse);
                    if (caseDocumentIDList != null)
                    {
                        int count = 0;
                        foreach (var fileData in caseDocumentIDList)
                        {
                            string newFileName = "";
                            Logger.WriteToLog("-------------------------- Iteration " + count + " --------------------------", LogLevel.Verbose);
                            GetDocumentResultEntity getDocumentResultEntity = GetDocument(fileData.DocumentID, fileData.DocumentEffectiveDate, fullNodePath);
                            if (getDocumentResultEntity != null)
                            {
                                newFileName = RenameFile(getDocumentResultEntity.FilePath[0], fileData);
                                WriteToCaseManifest(Context.Parameters.CaseNumber + "," + fileData.DocumentEffectiveDate + ","
                                + fileData.DocumentID + "," + newFileName + "," + "SUCCESS" + "," + "Document extracted successfully."
                                , fullNodePath);
                            }
                            count++;
                        }
                    }
                    else
                    {
                        Logger.WriteToLog("No documents are found for case number: " + Context.Parameters.CaseNumber, LogLevel.Verbose);
                        WriteToCaseManifest(Context.Parameters.CaseNumber + ",,,,,,No documents are found for the provided case number.", fullNodePath);
                    }
                }                
            }            
            catch (Exception e)
            {
                Logger.WriteToLog("Main Program Error: " + e, LogLevel.Verbose);
                Context.Errors.Add(new BaseCustomException(e.Message));
                WriteToCaseManifest(Context.Parameters.CaseNumber + ",,,,,There was an error: " + e.Message, fullNodePath);
            }
        
            // TODO: Handle errors we've collected during the job run.
            if (Context.Errors.Count > 0)
            {
                // Add a message to the job indicating that something went wrong.
                AddInformationToJob();

                // Collect errors, write them to a file, and attach the file to the job.
                LogErrors();
            }
            //Logger.WriteToLog("Trying to attach case manifest to Job Output. FilePath: " + fullNodePath + @"\" + Context.Parameters.ReportFolderName, LogLevel.Verbose);
            //AttachCaseManifestFileToJobOutput(fullNodePath + @"\" + Context.Parameters.ReportFolderName);
            ContinueWithProcessing("Job Completed Successfully");
        }

        private string ProcessCaseDocumentIDSearchTransaction(string nodeID)
        {
            TransactionEntity txn = new TransactionEntity();
            txn.TransactionType = "CaseDocumentExtractJob";
            txn.ReferenceNumber = "CaseDocumentExtractJob";
            txn.Source = "CaseDocumentExtractJob";

            // Create Data Propagation
            AddDataPropagationEntity dataPropagationEntity = new AddDataPropagationEntity();

            // 1. Adding CaseID
            txn.DataPropagation.AddIntraTxnDataPropagationEntry("#|CaseID|#", @"//TxnResponse/Result[@MessageType='FindCaseByCaseNumber']/CaseID");

            // 2. Adding Messages      

            try
            {
                // a. Find Case By Case Number
                Tyler.Odyssey.API.JobTemplate.FindCaseByCaseNumberEntity findCaseByCaseNumberEntity = new Tyler.Odyssey.API.JobTemplate.FindCaseByCaseNumberEntity();
                findCaseByCaseNumberEntity.SetStandardAttributes(1, "CaseDocumentExtractJob", Context.UserID, "CaseDocumentExtractJob", Context.SiteID);
                findCaseByCaseNumberEntity.NodeID = "1";                
                
                findCaseByCaseNumberEntity.CaseNumber = Context.Parameters.CaseNumber;
                if (!string.IsNullOrEmpty(nodeID))
                {
                    string[] singleNode = new string[1];
                    singleNode[0] = nodeID;
                    findCaseByCaseNumberEntity.SearchNodeID = singleNode;
                }
                
                txn.Messages.Add(findCaseByCaseNumberEntity);

                // b. Get Case Documents                
                GetDocumentInfoByEntity getDocumentInfoByEntity = new GetDocumentInfoByEntity();
                getDocumentInfoByEntity.SetStandardAttributes(0, "CaseDocumentExtractJob", Context.UserID, "CaseDocumentExtractJob", Context.SiteID);
                getDocumentInfoByEntity.ReferenceNumber = "CaseDocumentExtractJob";
                getDocumentInfoByEntity.Source = "CaseDocumentExtractJob";
                getDocumentInfoByEntity.UserID = Constants.systemUserID;
                getDocumentInfoByEntity.EntityID = "#|CaseID|#";
                getDocumentInfoByEntity.EntityType = DocumentByEntityEntityType.Case;
                txn.Messages.Add(getDocumentInfoByEntity);

                Logger.WriteToLog("GetDocID Transaction String: " + txn.ToOdysseyTransactionXML(), LogLevel.Verbose);

                // 3. Process Transaction   
                var blah = txn.ToOdysseyTransactionXML();
                string response = ProcessTransaction(txn.ToOdysseyTransactionXML());
                return response;
            }
            catch (Exception e)
            {
                Logger.WriteToLog("Error forming Document Transaction XML: " + e, LogLevel.Verbose);
                return null;
            }
        }

        private List<Entities.FileNameInfo> ExtractTransactionElements(string response)
        {

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(response);
            //XmlNodeList documentIdNodes = doc.SelectNodes("/TxnResponse/Result[@MessageType='GetDocumentInfoByEntity']/Documents/Document/CurrentDocumentVersionID");
            XmlNodeList documentNodes = doc.SelectNodes("/TxnResponse/Result[@MessageType='GetDocumentInfoByEntity']/Documents/Document");

            List<Entities.FileNameInfo> fileNameInfoList = new List<Entities.FileNameInfo>();
            
            foreach (XmlNode node in documentNodes)
            {
                Entities.FileNameInfo fileNameInfo = new Entities.FileNameInfo();
                // Get DocumentID
                fileNameInfo.DocumentID = node.SelectSingleNode("CurrentDocumentVersionID").InnerText;
                // Get Document Effective Date
                fileNameInfo.DocumentEffectiveDate = node.SelectSingleNode("DocumentVersions/DocumentVersion[DocumentVersionID=" 
                    + fileNameInfo.DocumentID + "]/EffectiveDate").InnerText;

                fileNameInfoList.Add(fileNameInfo);
            }
            return fileNameInfoList;
        }

        private string ExtractNodeID(string response)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(response);
            XmlNode nodeNode = doc.SelectSingleNode("/TxnResponse/Result[@MessageType='FindCaseByCaseNumber']/NodeID");

            return nodeNode.InnerText;
        }

        private string ExtractCaseType(string response)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(response);
            XmlNode nodeNode = doc.SelectSingleNode("/TxnResponse/Result[@MessageType='FindCaseByCaseNumber']/CaseType");

            return nodeNode.InnerText;
        }

        private string GetFilePath(string nodeID, string caseType = null)
        {
            if (caseType == "JD")
            {
                string filePath = Context.Parameters.BaseDocumentPath + @"\" + "Juvenile_Justice";
                Logger.WriteToLog("GetFilePath: " + filePath, LogLevel.Verbose);
                return Context.Parameters.BaseDocumentPath + @"\" + "Juvenile_Justice";
            }
            else if (caseType == "JA")
            {
                string filePath = Context.Parameters.BaseDocumentPath + @"\" + "Child_Protection";
                Logger.WriteToLog("GetFilePath: " + filePath, LogLevel.Verbose);
                return Context.Parameters.BaseDocumentPath + @"\" + "Child_Protection";
            }
            else
            {
                string filePath = Context.Parameters.BaseDocumentPath + @"\" + GetNodePath(nodeID);
                Logger.WriteToLog("GetFilePath: " + filePath, LogLevel.Verbose);
                return Context.Parameters.BaseDocumentPath + @"\" + GetNodePath(nodeID);
            }
        }

        private string GetNodePath(string nodeID)
        {
            //05072019 - Updated to only use the parent
            string finalNodeFilePath = "";

            // Initial Node Call
            string firstQuery = "SELECT TOP 1 NodeID, NodeIDParent, OrgUnitName, NodeLevel, OrgUnitTypeDescription"
                + " FROM Operations.dbo.fnGetNodeList('" + Context.SiteID + "')"
                + " WHERE NodeID = " + nodeID;

            //DataSet dataSet = GetSqlDataSet(nodeID, firstQuery);
            DataSet dataSet = GetSqlDataSet(firstQuery);
            DataTable dataTable = dataSet.Tables[0];

            Logger.WriteToLog($"DataTable Count: {dataTable.Rows.Count}", LogLevel.Verbose);

            if (dataTable.Rows.Count > 0)
            {
                
                DataRow row = dataTable.Rows[0];
                string nodeIDParent = row[1]?.ToString();
                var fileFolder = row[2]?.ToString();
                int nodeLevel = int.Parse(row[3]?.ToString());

                if (nodeLevel == 2)
                    finalNodeFilePath = fileFolder.Replace(" ", "_");
                else
                {
                    //finalNodeFilePath = fileFolder;
                    Logger.WriteToLog("Initial Node Level: " + nodeLevel, LogLevel.Verbose);
                    for (int i = nodeLevel; i >= 2; i--)
                    {
                        Logger.WriteToLog("Iteration: " + i, LogLevel.Verbose);
                        string subsequentQuery = "SELECT TOP 1 NodeID, NodeIDParent, OrgUnitName, NodeLevel, OrgUnitTypeDescription"
                            + " FROM Operations.dbo.fnGetNodeList('" + Context.SiteID + "')"
                            + " WHERE NodeID = " + nodeIDParent;
                        dataSet = GetSqlDataSet(subsequentQuery);
                        dataTable = dataSet.Tables[0];
                        row = dataTable.Rows[0];
                        fileFolder = row[2]?.ToString();
                        int childNodeLevel = int.Parse(row[3]?.ToString());

                        //finalNodeFilePath = fileFolder + @"\" + finalNodeFilePath;
                        if (childNodeLevel == 2)
                            finalNodeFilePath = fileFolder.Replace(" ", "_");

                        nodeIDParent = row[1]?.ToString();
                    }
                }
            }
            else
            {
                return null;
            }

            Logger.WriteToLog("GetNodePath Node Path: " + finalNodeFilePath, LogLevel.Verbose);

            return finalNodeFilePath;
        }

        private DataSet GetSqlDataSet(string query)
        {
            Logger.WriteToLog("Get SQL Data Set", LogLevel.Verbose);
            //string QUERY = createSQL(query);
            Logger.WriteToLog("Return from Create SQL.", LogLevel.Verbose);

            CDBBroker broker = new CDBBroker(Context.SiteID);
            var brokerConnection = broker.GetConnection("Justice");
            DataSet ret = null;
            //SqlCommand cmd = new SqlCommand(string.Format(QUERY), brokerConnection as SqlConnection);
            SqlCommand cmd = new SqlCommand(string.Format(query), brokerConnection as SqlConnection);

            try
            {
                Logger.WriteToLog("Trying SQL.", LogLevel.Verbose);
                cmd.Connection.Open();
                ret = CDBBroker.LoadDataSet(Context.SiteID, cmd, true);
            }
            finally
            {
                if (cmd != null)
                {
                    cmd.Connection.Close();
                }
            }

            return ret;
        }

        /*
        private string createSQL(string query)
        {
            Logger.WriteToLog("Create SQL", LogLevel.Verbose);
            
            string sql = 
                "SELECT TOP 1 NodeID, NodeIDParent, OrgUnitName, NodeLevel, OrgUnitTypeDescription"
                + " FROM Operations.dbo.fnGetNodeList('" + Context.SiteID + "')"
                + " WHERE NodeID = " + nodeID;
             
            string sql = query;

            return sql;
        }
        */

        private GetDocumentResultEntity GetDocument(string CurrentDocumentVersionID, string DocumentDate, string fullNodePath)
        {

            Logger.WriteToLog("GetDocument - FullNodePath: " + fullNodePath, LogLevel.Verbose);

            try
            {
                // Create GetDocument API
                Tyler.Odyssey.API.JobTemplate.GetDocumentEntity entity = new Tyler.Odyssey.API.JobTemplate.GetDocumentEntity();
                entity.SetStandardAttributes(0, "GetDocument", Context.UserID, "GetDocument", Context.SiteID);
                entity.ReferenceNumber = "CaseDocumentExtractJob";
                entity.Source = "CaseDocumentExtractJob";
                entity.UserID = Constants.systemUserID;
                entity.ItemElementName = Tyler.Odyssey.API.JobTemplate.ItemChoiceType.FilePath;

                // UPDATE THIS
                entity.Item = fullNodePath + @"\" + Context.Parameters.InputFolderName;

                entity.VersionID = CurrentDocumentVersionID;
                Logger.WriteToLog("GetDocumentXML String: " + entity.ToOdysseyMessageXml(), LogLevel.Verbose);
                OdysseyMessage msg = new OdysseyMessage(entity.ToOdysseyMessageXml(), Context.SiteID);

                // Process GetDocument API
                MessageHandlerFactory.Instance.ProcessMessage(msg);
                StringReader reader = new StringReader(msg.ResponseDocument.OuterXml);
                XmlSerializer serializer = new XmlSerializer(typeof(GetDocumentResultEntity));
                GetDocumentResultEntity result = (GetDocumentResultEntity)serializer.Deserialize(reader);
                
                return result;
            }
            catch (Exception e)
            {
                Logger.WriteToLog("There was an error extracting current document version ID: " + CurrentDocumentVersionID + ". Error: " + e, LogLevel.Verbose);
                Context.Errors.Add(new BaseCustomException(e.Message));
                WriteToCaseManifest(
                    Context.Parameters.CaseNumber + "," 
                    + DocumentDate + "," 
                    + CurrentDocumentVersionID + ","
                    + ","
                    + "FAILED" + "," 
                    + "There was an error extracting document: " + e.Message
                    , fullNodePath);
                return null;
            }
        }

        private string RenameFile(string fullFilePath, Entities.FileNameInfo fileData)
        {
            Logger.WriteToLog("Rename File." + fullFilePath, LogLevel.Verbose);
            try
            {
                string fileLocation = Path.GetDirectoryName(fullFilePath);
                string fileExtension = Path.GetExtension(fullFilePath);

                //mmddyyyy_case#_docId              
                DateTime docEffectiveDate = DateTime.ParseExact(fileData.DocumentEffectiveDate, "MM/dd/yyyy", null);

                //string eventDate = docEffectiveDate.ToString("MMddyyyy"); //Old

                Logger.WriteToLog("This is the Document ID: " + fileData.DocumentID, LogLevel.Verbose);
                
                string eventDate = GetDocumentEventDate(fileData.DocumentID);

                string newFileName = eventDate + "_" + Context.Parameters.CaseNumber
                    + "_" + fileData.DocumentID.ToString();
                Logger.WriteToLog("Current File Name: " + fullFilePath, LogLevel.Verbose);

                if (!File.Exists(fileLocation + @"\" + newFileName + fileExtension))
                {
                    File.Move(fullFilePath, fileLocation + @"\" + newFileName + fileExtension);
                    Logger.WriteToLog("New File Name: " + fileLocation + @"\" + newFileName + fileExtension, LogLevel.Verbose);
                    return newFileName + fileExtension;
                }
                else
                {
                    string timestamp = DateTime.Now.ToString("MMddyyyyHHmmssFFF");
                    File.Move(fullFilePath, fileLocation + @"\" + newFileName + "_" + timestamp + fileExtension);
                    Logger.WriteToLog("New File Name: " + fileLocation + @"\" + newFileName + "_" + timestamp + fileExtension, LogLevel.Verbose);
                    return newFileName + "_" + timestamp + fileExtension;
                }
            }
            catch (Exception e)
            {
                Logger.WriteToLog("There was an error trying to rename the file: " + e, LogLevel.Verbose);
                return null;
            }
        }

        private string GetDocumentEventDate(string documentID)
        {
            string eventDate;
            Logger.WriteToLog("Creating query", LogLevel.Verbose);
            string query = @"SELECT TOP 1 FORMAT(CE.EventDate, 'MMddyyyy') as [CaseEvent]"
                + " FROM Operations.dbo.DocVersion DV WITH(NOLOCK)"
                + " JOIN Operations.dbo.ParentLink PL WITH(NOLOCK) ON PL.DocumentID = DV.DocumentID AND PL.ParentTypeID IN(2,10)"
                + " JOIN Justice.dbo.CaseEvent CE WITH(NOLOCK) ON CE.EventID = PL.ParentID"
                + " WHERE DV.DocumentVersionID = " + documentID;
            try
            {
                DataSet eventDateDS = GetSqlDataSet(query);                
                DataTable dataTable = eventDateDS.Tables[0];
                DataRow row = dataTable.Rows[0].IsNull(0) ? null : dataTable.Rows[0];
                eventDate = row[0].ToString();
            }
            catch (Exception e)
            {
                Logger.WriteToLog(e.ToString(), LogLevel.Verbose);
                eventDate = "NoEvent";
            }
            Logger.WriteToLog("EventDate: " + eventDate, LogLevel.Verbose);
            return eventDate;
        }

        // Process Transaction
        public string ProcessTransaction(string transXml)
        {
            string txnResults = string.Empty;

            try
            {
                OdysseyTransaction txn = new OdysseyTransaction(0, transXml, Context.SiteID);
                TransactionProcessor txnProcessor = new TransactionProcessor();
                txnProcessor.ProcessTransaction(txn);

                if (txn.TransactionRejected)
                    throw new Exception(txn.RejectReason);
                else
                  if (txn.ResponseDocument != null)
                    txnResults = txn.ResponseDocument.OuterXml;
            }
            // if a schema exceptions is thrown, then throw the new exception with the data from the schema error.
            catch (SchemaValidationException svex)
            {
                throw new Exception(svex.ReplacementStrings[0]);                
            }
            catch (DataConversionException dcex)
            {
                // check for an xslCodeQuery exception type in the inner exception
                // so we can report a better, more descriptive error 
                if (dcex.InnerException.GetType().Equals(typeof(XslCodeQueryException)))
                {
                    XslCodeQueryException xcqe = (XslCodeQueryException)dcex.InnerException;
                    throw new Exception(xcqe.ReplacementStrings[0], dcex);
                }
                else
                {
                    throw new Exception(dcex.Message);
                }

            }
            catch (Exception ex)
            {
                Logger.WriteToLog("ERROR: " + ex.ToString(), LogLevel.Verbose);
                Context.Errors.Add(new BaseCustomException(ex.ToString()));
                throw ex;
            }

            return txnResults;
        }

        private void AddInformationToJob()
        {
            int jobTaskID = 0;
            int jobProcessID = 0;

            if (Int32.TryParse(Context.taskID, out jobTaskID) && Int32.TryParse(Context.jobProcessID, out jobProcessID))
            {
                object Parms = new object[,] { { "SEVERITY" }, { "2" } };

                ITYLJobTaskUtility taskUtility = (JobProcessingInterface.ITYLJobTaskUtility)Activator.CreateInstance(Type.GetTypeFromProgID("Tyler.Odyssey.JobProcessing.TYLJobTaskUtility.cTask"));

                taskUtility.AddTextMessage(Context.SiteID, jobProcessID, jobTaskID, "The job completed successfully, but errors occurred. Please see the log file for the errors associated with the job.", ref Parms);
            }
        }

        private void LogErrors()
        {
            using (StreamWriter writer = GetTempFile())
            {
                Logger.WriteToLog("Beginning to write to temp file.", LogLevel.Intermediate);

                // Write the file header
                writer.WriteLine("CaseNumber,CaseID,CaseFlag,Error");

                // For each error, write some information.
                Context.Errors.ForEach((BaseCustomException f) => WriteErrorToLog(f, writer));

                Logger.WriteToLog("Finished writing to temp file.", LogLevel.Intermediate);

                AttachTempFileToJobOutput(writer, @"Add Remove Case Flags Action - Errors");
            }
        }

        private void WriteErrorToLog(BaseCustomException exception, StreamWriter writer)
        {
            writer.WriteLine(string.Format("\"{0}\"", exception.CustomMessage));
        }

        private StreamWriter GetTempFile()
        {
            if (TaskUtility == null)
                return null;

            string filePath = TaskUtility.GenerateFile(Context.SiteID, ref taskParms);
            StreamWriter fileWriter = new StreamWriter(filePath, true);

            Logger.WriteToLog("Created temp file at location: " + filePath, LogLevel.Basic);

            return fileWriter;
        }

        private void AttachTempFileToJobOutput(StreamWriter writer, string errorFileName)
        {
            Logger.WriteToLog("Begining AttachTempFileToJobOutput()", LogLevel.Intermediate);
            Logger.WriteToLog(writer == null ? "File is NULL" : "File is NOT NULL", LogLevel.Intermediate);

            if (writer != null && TaskUtility != null)
            {
                FileStream fileStream = writer.BaseStream as FileStream;
                string filePath = fileStream.Name;
                Logger.WriteToLog("File Path: " + filePath, LogLevel.Intermediate);

                writer.Close();

                if (filePath.Length > 0 && errorFileName.Length > 0)
                    AttachFile(filePath, errorFileName);

                Logger.WriteToLog("Completed AttachTempFileToJobOutput()", LogLevel.Intermediate);
            }
        }

        private void AttachFile(string filepath, string filename)
        {
            DataProcessor.Logger.WriteToLog("Begin AttachFile()", Tyler.Odyssey.Utils.LogLevel.Intermediate);
            int nodeID = 0;
            int taskIDInt = 0;
            int jobProcessIDInt = 0;

            if (TaskUtility != null)
            {
                if (Int32.TryParse(Context.taskID, out taskIDInt) && Int32.TryParse(Context.jobProcessID, out jobProcessIDInt))
                {
                    int documentID = TaskUtility.AddOutputDocument(this.siteKey, taskIDInt, jobProcessIDInt, -1, filepath, Context.UserID, nodeID, ref taskParms);

                    if (documentID > 0)
                    {
                        TaskUtility.AddOutputParams(this.siteKey, taskIDInt, "TEXT", documentID, filename, TaskDocument, ref taskParms);

                        TaskUtility.DeleteTempFile(filepath);

                        this.OutputJobTaskXML = TaskDocument.documentElement.xml;
                    }
                }
            }

            DataProcessor.Logger.WriteToLog("End Attach()", Tyler.Odyssey.Utils.LogLevel.Intermediate);
        }

        // Casemanifest logging logic.
        private void AttachFileDontDeleteTemp(string fullNodePath)
        {
            DataProcessor.Logger.WriteToLog("Begin AttachFile()", Tyler.Odyssey.Utils.LogLevel.Intermediate);
            int nodeID = 0;
            int taskIDInt = 0;
            int jobProcessIDInt = 0;
            string fileName = Path.GetFileName(fullNodePath);

            if (TaskUtility != null)
            {
                if (Int32.TryParse(Context.taskID, out taskIDInt) && Int32.TryParse(Context.jobProcessID, out jobProcessIDInt))
                {
                    int documentID = TaskUtility.AddOutputDocument(this.siteKey, taskIDInt, jobProcessIDInt, -1, fullNodePath, Context.UserID, nodeID, ref taskParms);

                    if (documentID > 0)
                    {
                        TaskUtility.AddOutputParams(this.siteKey, taskIDInt, "TEXT", documentID, fileName, TaskDocument, ref taskParms);
                        //TaskUtility.DeleteTempFile(filepath);
                        // File Created
                        DataProcessor.Logger.WriteToLog("File attached to job.", Tyler.Odyssey.Utils.LogLevel.Intermediate);

                        this.OutputJobTaskXML = TaskDocument.documentElement.xml;
                    }
                }
            }

            DataProcessor.Logger.WriteToLog("End Attach()", Tyler.Odyssey.Utils.LogLevel.Intermediate);
        }

        private void WriteToCaseManifest(string text, string fullNodePath)
        {
            string pathWithReportFolder = fullNodePath + @"\Report\" + "CaseImageExtractReport_" 
                + DateTime.Now.ToString("yyyyMMdd") + ".csv";
            
            if (!File.Exists(pathWithReportFolder))
            {
                Logger.WriteToLog("New Case Manifest. Path: " + pathWithReportFolder, LogLevel.Verbose);
                using (var tw = new StreamWriter(pathWithReportFolder, true))
                {
                    // This should always be the header.
                    var header = "Timestamp,Case Number,Document Date,Document Version ID,File Name,Status,Message";
                    tw.WriteLine(header);

                    var newLine = text;
                    tw.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "," + newLine);
                }
            }
            else
            {
                Logger.WriteToLog("Existing Case Manifest. Path: " + pathWithReportFolder, LogLevel.Verbose);
                var newLine = string.Format("{0},{1}", DateTime.Now.ToString("HH:mm:ss"), text);
                File.AppendAllText(pathWithReportFolder, newLine + Environment.NewLine);
            }
        }

        private void AttachCaseManifestFileToJobOutput(string fullNodePath)
        {
            string fileName = Path.GetFileName(fullNodePath);
            Logger.WriteToLog("AttachCaseManifestFileToJobOutput. fullNodePath: " + fullNodePath, LogLevel.Verbose);
            Logger.WriteToLog("AttachCaseManifestFileToJobOutput. fileName: " + fileName, LogLevel.Verbose);

            if (fileName.Length > 0)
                AttachFileDontDeleteTemp(fullNodePath + @"\" + "CaseImageExtractReport_" + DateTime.Now.ToString("yyyyMMdd") + ".csv");
        }

        private void AddCaseManifestInfoToJobOutput()
        {
            int jobTaskID = 0;
            int jobProcessID = 0;
            Logger.WriteToLog("Task ID: " + Context.taskID + "; Job ProcessID: " + Context.jobProcessID, LogLevel.Basic);

            if (Int32.TryParse(Context.taskID, out jobTaskID) && Int32.TryParse(Context.jobProcessID, out jobProcessID))
            {
                object Parms = new object[,] { { "SEVERITY" }, { "1" } };

                ITYLJobTaskUtility taskUtility = (JobProcessingInterface.ITYLJobTaskUtility)Activator.CreateInstance(Type.GetTypeFromProgID("Tyler.Odyssey.JobProcessing.TYLJobTaskUtility.cTask"));

                taskUtility.AddTextMessage(Context.SiteID, jobProcessID, jobTaskID, "The job completed successfully.  Case log file is attached.", ref Parms);
            }
        }
    }
}