using System;

namespace contentapi.data.Views
{
    [ResultFor(RequestType.activity_aggregate)]
    [SelectFrom("content_history h join content c on h.contentId = c.id")]
    [GroupBy("h.contentId, h.createUserId")]
    [ExtraQueryField("id", "h.id")]
    [ExtraQueryField("createDate", "h.createDate")]
    [ExtraQueryField("contentType", "c.contentType")]
    public class ActivityAggregateView
    {
        [DbField("h.contentId")]
        public long contentId { get; set; }

        [DbField("h.createUserId")]
        public long createUserId { get; set; }


        [DbField("count(h.id)")]
        public long count { get; set; }

        [NoQuery]
        [DbField("max(h.id)")]
        public long maxId { get; set; }

        [NoQuery]
        [DbField("min(h.id)")]
        public long minId { get; set; }

        [NoQuery]
        [DbField("max(h.createDate)")]
        public DateTime maxCreateDate { get; set; }

        [NoQuery]
        [DbField("min(h.createDate)")]
        public DateTime minCreateDate { get; set; }
    }
}
