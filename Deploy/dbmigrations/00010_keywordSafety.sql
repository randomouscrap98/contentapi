BEGIN TRANSACTION;

update content_keywords set value = REPLACE(value, ' ', '_');
update content_keywords set value = REPLACE(value, '"', '''');
delete from content_keywords where value = '';

COMMIT;