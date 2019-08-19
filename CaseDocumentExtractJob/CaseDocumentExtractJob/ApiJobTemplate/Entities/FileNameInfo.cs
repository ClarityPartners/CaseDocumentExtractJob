using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CaseDocumentExtractJob.Entities
{
    public class FileNameInfo
    {
        private string documentID;
        private string documentEffectiveDate;

        public string DocumentID { get => documentID; set => documentID = value; }
        public string DocumentEffectiveDate { get => documentEffectiveDate; set => documentEffectiveDate = value; }
    }
}
