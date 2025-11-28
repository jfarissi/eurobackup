using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Backup.Web.Api.Server.Models;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial interface IStorageBroker
    {
        // Relations
        ValueTask<DocumentRelation> InsertRelationAsync(DocumentRelation relation);
        IQueryable<DocumentRelation> SelectAllRelations();
        ValueTask DeleteRelationAsync(int relationId);
    }
}
