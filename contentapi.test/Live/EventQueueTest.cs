using System.Collections.Concurrent;
using AutoMapper;
using contentapi.Live;
using contentapi.Main;
using contentapi.Search;
using contentapi.Utilities;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

public class EventQueueTest : UnitTestBase, IClassFixture<DbUnitTestSearchFixture>
{
    protected DbWriter writer;
    protected IGenericSearch searcher;
    protected DbUnitTestSearchFixture fixture;
    protected IMapper mapper;
    protected EventQueue queue;
    protected ICacheCheckpointTracker<EventData> tracker;
    protected IPermissionService permission;
    protected EventQueueConfig config;
    //protected ConcurrentDictionary<int, AnnotatedCacheItem> trueCache;

    //The tests here are rather complicated; we can probably simplify them in the future, but for now,
    //I just need a system that REALLY tests if this whole thing works, and that is most reliable if 
    //I just use the (known to work) dbwriter to set up the database in a way we expect.
    public EventQueueTest(DbUnitTestSearchFixture fixture)
    {
        this.fixture = fixture;
        this.mapper = fixture.GetService<IMapper>();
        this.tracker = fixture.GetService<ICacheCheckpointTracker<EventData>>();
        this.searcher = fixture.GetService<IGenericSearch>();
        this.permission = fixture.GetService<IPermissionService>();
        this.config = new EventQueueConfig();
        //this.trueCache = new ConcurrentDictionary<int, AnnotatedCacheItem>();
        this.queue = new EventQueue(fixture.GetService<ILogger<EventQueue>>(), this.config, this.tracker, () => this.searcher, this.permission); //, this.trueCache);
        writer = new DbWriter(fixture.GetService<ILogger<DbWriter>>(), this.searcher, 
            fixture.GetService<Db.ContentApiDbConnection>(), fixture.GetService<ITypeInfoService>(), this.mapper,
            fixture.GetService<Db.History.IHistoryConverter>(), this.permission, this.queue); 

        //Reset it for every test
        fixture.ResetDatabase();
    }

    //First, without actually doing anything with the event part, ensure the core of the service works. Does
    //building a request for events create something we expect?
}