using Backup.Web.Api.Server.Models.WordDocumentContents;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Backup.Web.Api.Server.Models.WordDocuments
{
    public partial class WordDocument
    {
        private static readonly char delimiter = ',';
        public Guid Id { get; set; }
            public string Name { get; set; }
            public string Url { get; set; }
            public int CountPages { get; set; }
            public DateTimeOffset CreatedDate { get; set; }
            public DateTimeOffset UpdatedDate { get; set; }
            public Guid CreatedBy { get; set; }
            public Guid UpdatedBy { get; set; }
            public Guid UserId { get; set; }
            public string Suggestions { get; set; }
            public string Settings { get; set; }
            public string Domaines { get; set; }
        public string Ext { get; set; }
        public string FileName { get; set; }
        [NotMapped]
        public string[] Tags
        {
            get { return Suggestions.Split(delimiter); }
            set
            {
                ;// _tags = string.Join($"{delimiter}", value);
            }
        }
        public IEnumerable<WordDocumentContent> WordDocumentContents { get; set; }
    }
}
