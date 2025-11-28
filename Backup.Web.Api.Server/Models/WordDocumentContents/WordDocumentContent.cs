using Backup.Web.Api.Server.Models.WordDocuments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Backup.Web.Api.Server.Models.WordDocumentContents
{
    public class WordDocumentContent
    {
        public Guid Id { get; set; }
        public string Page { get; set; }
        //word,pdf et powerpoint
        public string Content { get; set; }
        //excel
        public string Cell { get; set; }
        public string CustumValue { get; set; }

        public string Value { get; set; }
        public Guid WordDocumentId { get; set; }
        public WordDocument WordDocument { get; set; }
    }
}
