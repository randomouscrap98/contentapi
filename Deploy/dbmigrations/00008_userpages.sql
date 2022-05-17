-- Moving userpages to real types
update content set contentType = 4 where contentType = 1 and literalType = 'userpage';
