-- I accidentally set edit dates to 0 rather than null
update content_watches set editDate = NULL where editDate LIKE '0001%';