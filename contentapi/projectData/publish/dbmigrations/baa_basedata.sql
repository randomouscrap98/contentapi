insert into entities(createDate, status, baseAllow) 
 values ('2019-01-01 00:00:00.000000', 0, 6);
insert into categoryEntities(entityId, name) 
 values ((select id from entities where createDate='2019-01-01 00:00:00.000000'), 'sbs-main');