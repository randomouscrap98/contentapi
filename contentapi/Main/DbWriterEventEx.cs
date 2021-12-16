//using AutoMapper;
//using contentapi.Db;
//using contentapi.Db.History;
//using contentapi.Search;
//using contentapi.Utilities;
//
//namespace contentapi.Main;
//
//public class DbWriterEventEx : DbWriter
//{
//    public DbWriterEventEx(
//        ILogger<DbWriter> logger, 
//        IGenericSearch searcher, 
//        ContentApiDbConnection connection, 
//        ITypeInfoService typeInfoService, 
//        IMapper mapper, 
//        IHistoryConverter historyConverter, 
//        IPermissionService permissionService) 
//    : base(logger, searcher, connection, typeInfoService, mapper, historyConverter, permissionService)
//    {
//    }
//
//    public override async Task<T> DeleteAsync<T>(long id, long requestUserId, string? message = null)
//    {
//        var result = await base.DeleteAsync<T>(id, requestUserId, message);
//
//        return result;
//    }
//
//    public override async Task<T> WriteAsync<T>(T view, long requestUserId, string? message = null)
//    {
//        var result = await base.WriteAsync(view, requestUserId, message);
//
//        //Create event here
//
//        return result;
//    }
//}