using System;
using System.Threading.Tasks;
using contentapi.Models;

namespace contentapi.Services
{
    public class ActionLogService
    {
        //public bool DoActionLog = true;

        //public async Task LogAct(LogAction action, Action<ActionLog> setField)
        //{
        //    //Do NOT LOG if we're not set to
        //    if(!DoActionLog)
        //        return;

        //    var log = new ActionLog()
        //    {
        //        action = action,
        //        createDate = DateTime.Now,
        //        contentId = null,
        //        categoryId = null,
        //        userId = null
        //    };

        //    try
        //    {
        //        log.actionUserId = GetCurrentUid();
        //    }
        //    catch
        //    {
        //        //Eventually we can log here... when are we adding logging?
        //        return;
        //    }

        //    setField(log);

        //    await context.Logs.AddAsync(log);
        //    await context.SaveChangesAsync();
        //}

        //protected async Task LogAct(LogAction action, long id)
        //{
        //    await LogAct(action, (l) => SetLogField(l, id));
        //}

    }
}