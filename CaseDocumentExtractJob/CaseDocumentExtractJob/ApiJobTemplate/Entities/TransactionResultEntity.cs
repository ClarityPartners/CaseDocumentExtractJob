namespace Tyler.Odyssey.API.JobTemplate
{
    using System;
    using System.Collections.Generic;
    using System.Xml.Serialization;

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.6.81.0")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "", ElementName = "TxnResponse", IsNullable = false)]
    public partial class TransactionResultEntity
    {


        private List<Result> result;

        public List<Result> ResultList { get => result; set => result = value; }

        [XmlRoot(ElementName = "Result")]
        public class Result
        {
            [XmlElement(ElementName = "NodeID")]
            public string NodeID { get; set; }
            [XmlElement(ElementName = "CaseID")]
            public string CaseID { get; set; }
            [XmlElement(ElementName = "CaseNumber")]
            public string CaseNumber { get; set; }
            [XmlElement(ElementName = "CaseStyle")]
            public string CaseStyle { get; set; }
            [XmlElement(ElementName = "CaseStatus")]
            public string CaseStatus { get; set; }
            [XmlElement(ElementName = "CaseType")]
            public string CaseType { get; set; }
            [XmlAttribute(AttributeName = "MessageType")]
            public string MessageType { get; set; }
            [XmlElement(ElementName = "Documents")]
            public Documents Documents { get; set; }

        }

        [XmlRoot(ElementName = "DocumentFragment")]
        public class DocumentFragment
        {
            [XmlElement(ElementName = "DocumentFragmentID")]
            public string DocumentFragmentID { get; set; }
            [XmlElement(ElementName = "FragmentType")]
            public string FragmentType { get; set; }
            [XmlElement(ElementName = "SequenceNumber")]
            public string SequenceNumber { get; set; }
            [XmlElement(ElementName = "Extension")]
            public string Extension { get; set; }
        }

        [XmlRoot(ElementName = "DocumentFragments")]
        public class DocumentFragments
        {
            [XmlElement(ElementName = "DocumentFragment")]
            public DocumentFragment DocumentFragment { get; set; }
        }

        [XmlRoot(ElementName = "DocumentVersion")]
        public class DocumentVersion
        {
            [XmlElement(ElementName = "DocumentVersionID")]
            public string DocumentVersionID { get; set; }
            [XmlElement(ElementName = "Description")]
            public string Description { get; set; }
            [XmlElement(ElementName = "EffectiveDate")]
            public string EffectiveDate { get; set; }
            [XmlElement(ElementName = "PageCount")]
            public string PageCount { get; set; }
            [XmlElement(ElementName = "RedactionStatus")]
            public string RedactionStatus { get; set; }
            [XmlElement(ElementName = "ReadOnlyFlag")]
            public string ReadOnlyFlag { get; set; }
            [XmlElement(ElementName = "DocumentFragments")]
            public DocumentFragments DocumentFragments { get; set; }
            [XmlElement(ElementName = "DocumentSecurityGroup")]
            public string DocumentSecurityGroup { get; set; }
        }

        [XmlRoot(ElementName = "DocumentVersions")]
        public class DocumentVersions
        {
            [XmlElement(ElementName = "DocumentVersion")]
            public DocumentVersion DocumentVersion { get; set; }
        }

        [XmlRoot(ElementName = "Document")]
        public class Document
        {
            [XmlElement(ElementName = "DocumentID")]
            public string DocumentID { get; set; }
            [XmlElement(ElementName = "DocumentType")]
            public string DocumentType { get; set; }
            [XmlElement(ElementName = "DocumentName")]
            public string DocumentName { get; set; }
            [XmlElement(ElementName = "SecurityToken")]
            public string SecurityToken { get; set; }
            [XmlElement(ElementName = "CurrentDocumentVersionID")]
            public string CurrentDocumentVersionID { get; set; }
            [XmlElement(ElementName = "DocumentCategory")]
            public string DocumentCategory { get; set; }
            [XmlElement(ElementName = "DocumentVersions")]
            public DocumentVersions DocumentVersions { get; set; }
        }

        [XmlRoot(ElementName = "Documents")]
        public class Documents
        {
            [XmlElement(ElementName = "Document")]
            public List<Document> Document { get; set; }
        }


    }
}