using System;
using System.Collections.Generic;
using System.Linq;
using Dapper.Contrib.Extensions;
using Microsoft.Extensions.Logging;

namespace contentapi.test;

public enum UserVariations
{
    Super = 1,
    Variables = 2,
    Special = 4,
    Registered = 8
}

public enum ContentVariations
{
    AccessBySupers = 1,
    AccessByAll = 2,
    Values = 4,
    Keywords = 8,
    Comments = 16,
    Deleted = 32,
}

public class DbUnitTestSearchFixture : DbUnitTestBase, IDisposable
{
    //public const int basicUser = 1;
    //public const int adminUser = 2;
    //public const int carlUser = 3;
    //public const int basicPage = 1;
    //public const int adminPage = 2;

    public readonly List<string> StandardKeywords = new List<string> {
        "one",
        "two",
        "three",
        "four",
        "five",
        "six",
        "seven",
        "eight"
    };

    public readonly int UserCount;
    public readonly int ContentCount;

    public DbUnitTestSearchFixture()
    {
        logger.LogDebug($"Adding users to database {MasterConnectionString}");

        //Create tables for the in-memory database we're testing against
        //for this particular test. Note that ALL tests that use this 
        //fixture will get the SAME database!
        using(var conn = CreateNewConnection())
        {
            using(var tsx = conn.BeginTransaction())
            {
                var users = new List<Db.User>();
                var userVariables = new List<Db.UserVariable>();
                UserCount = (int)Math.Pow(2, Enum.GetValues<UserVariations>().Count());

                for(var i = 0; i < UserCount; i++)
                {
                    var user = new Db.User() {
                        username = $"user_{i}",
                        password = $"SECRETS_{i}",
                        createDate = DateTime.Now.AddDays(i - UserCount),
                        salt = $"SALTYSECRETS_{i}"
                    };

                    user.super = (i & (int)UserVariations.Super) > 0;
                    user.special = (i & (int)UserVariations.Special) > 0 ? $"special_{i}" : null;
                    user.registrationKey = (i & (int)UserVariations.Registered) > 0 ? null : $"registration_{i}";

                    if((i & (int)UserVariations.Variables) > 0)
                    {
                        //Variable count should equal user id
                        for(var j = 0; j <= i; j++)
                        {
                            userVariables.Add(new Db.UserVariable()
                            {
                                userId = i + 1,
                                key = $"userval_{j}_{i}",
                                value = $"value_{j}"
                            });
                        }
                    }

                    users.Add(user);
                }

                conn.Insert(users, tsx);
                conn.Insert(userVariables, tsx);

                var content = new List<Db.Content>();
                var comments = new List<Db.Comment>();
                var values = new List<Db.ContentValue>();
                var keywords = new List<Db.ContentKeyword>();
                var permissions = new List<Db.ContentPermission>();
                ContentCount = (int)Math.Pow(2, Enum.GetValues<ContentVariations>().Count());

                for(var i = 0; i < ContentCount; i++)
                {
                    var c = new Db.Content() {
                        name = $"content_{i}",
                        parentId = i / 4,
                        content = $"text_{i}",
                        createUserId = 1 + (i % UserCount),
                        createDate = DateTime.Now.AddDays(i - ContentCount)
                    };

                    c.deleted = (i & (int)ContentVariations.Deleted) > 0;
                    c.internalType = (Db.InternalContentType)(i % 4);//(i & (int)ContentVariations.PageOrFile) > 0 ? Db.InternalContentType.page : Db.InternalContentType.file;

                    if((i & (int)ContentVariations.AccessBySupers) > 0)
                    {
                        permissions.Add(new Db.ContentPermission()
                        {
                            contentId = i + 1,
                            userId = 1 + ((i | (int)UserVariations.Super) % UserCount), //User is contentid with super bit flip
                            create = true,
                            read = true,
                            update = true,
                            delete = true
                        });
                    }
                    
                    if((i & (int)ContentVariations.AccessByAll) > 0)
                    {
                        permissions.Add(new Db.ContentPermission()
                        {
                            contentId = i + 1,
                            userId = 0,
                            create = true,
                            read = true,
                            update = true,
                            delete = true
                        });
                    }

                    if((i & (int)ContentVariations.Values) > 0)
                    {
                        //Value count should equal contentId, yes even if it's a crapload
                        for(var j = 0; j <= i; j++)
                        {
                            values.Add(new Db.ContentValue()
                            {
                                contentId = i + 1,
                                key = $"contentval_{j}_{i}",
                                value = $"value_{j}"
                            });
                        }
                    }

                    if((i & (int)ContentVariations.Keywords) > 0)
                    {
                        //You get the keywords for 1/8 of the id
                        for(var j = 0; j <= i/8; j++)
                        {
                            keywords.Add(new Db.ContentKeyword()
                            {
                                contentId = i + 1,
                                value = StandardKeywords[j % StandardKeywords.Count]
                            });
                        }
                    }

                    if((i & (int)ContentVariations.Comments) > 0)
                    {
                        //comment count should equal contentId, yes even if it's a crapload
                        for(var j = 0; j <= i; j++)
                        {
                            comments.Add(new Db.Comment()
                            {
                                contentId = i + 1,
                                createDate = DateTime.Now.AddDays(j - i),
                                createUserId = 1 + (j % UserCount),
                                text = $"comment_{i}",
                            });
                        }
                    }

                    content.Add(c);
                }

                conn.Insert(content, tsx);
                conn.Insert(comments, tsx);
                conn.Insert(values, tsx);
                conn.Insert(keywords, tsx);
                conn.Insert(permissions, tsx);

                //var users = new List<Db.User> {
                //    new Db.User() {
                //        username = "firstUser",
                //        password = "shouldNotBeSearchable",
                //        salt = "alsoShouldNotBeSearchable",
                //        special = "",
                //        createDate = DateTime.Now.AddDays(-10),
                //        avatar = 99,
                //        email = "secrets@email.com"
                //    },
                //    new Db.User() {
                //        username = "admin",
                //        password = "shouldNotBeSearchable",
                //        salt = "alsoShouldNotBeSearchable",
                //        special = "cutenickname",
                //        createDate = DateTime.Now.AddDays(-15),
                //        avatar = 1,
                //        super = true,
                //        email = "admin@email.com"
                //    },
                //    new Db.User() {
                //        username = "carl",
                //        password = "differentpassword",
                //        salt = "verysalty",
                //        createDate = DateTime.Now,
                //        avatar = 0,
                //        email = "carl@karl.com"
                //    },
                //};

                //var content = new List<Db.Content> {
                //    new Db.Content() {
                //        name = "A test page",
                //        createDate = DateTime.Now.AddDays(-9),
                //        publicType = "test",
                //        internalType = Db.InternalContentType.page,
                //        createUserId = basicUser,
                //        content = "A long time ago, someone tried to test"
                //    },
                //    new Db.Content() {
                //        name = "Private room",
                //        createDate = DateTime.Now.AddDays(-19),
                //        publicType = "secrets",
                //        internalType = Db.InternalContentType.page,
                //        createUserId = adminUser,
                //        content = "The whole operation is run by Tony Lazuto"
                //    },
                //    new Db.Content() {
                //        createDate = DateTime.Now.AddDays(-5),
                //        internalType = Db.InternalContentType.file,
                //        createUserId = basicUser,
                //        publicType = "mybucket",
                //        content = "img/jpeg"
                //    },
                //    new Db.Content() {
                //        name = "carl's special pic",
                //        createDate = DateTime.Now.AddDays(-7),
                //        internalType = Db.InternalContentType.file,
                //        createUserId = carlUser,
                //        content = "img/png"
                //    },
                //    new Db.Content() {
                //        name = "pm",
                //        createDate = DateTime.Now.AddDays(-70),
                //        internalType = Db.InternalContentType.module,
                //        createUserId = adminUser,
                //        content = "--some lua code\nyeah"
                //    }
                //};

                //var permissions = new List<Db.ContentPermission> {
                //    new Db.ContentPermission()
                //    {
                //        contentId = basicPage,
                //        userId = 0,
                //        create = true, read = true
                //    },
                //    new Db.ContentPermission()
                //    {
                //        contentId = adminPage,
                //        userId = adminUser,
                //        create = true, update = true, read = true, delete = true
                //    },
                //    new Db.ContentPermission()
                //    {
                //        contentId = adminPage,
                //        userId = carlUser,
                //        create = true, read = true
                //    }
                //};

                //var comments = new List<Db.Comment> {
                //    new Db.Comment() {
                //        createUserId = carlUser,
                //        contentId = adminPage,
                //        createDate = DateTime.Now.AddDays(-1),
                //        text = "hi i'm paul"
                //    },
                //    new Db.Comment() {
                //        createUserId = adminUser,
                //        contentId = basicPage,
                //        createDate = DateTime.Now,
                //        text = "yes I agree"
                //    }
                //};

                ////We're making ASSUMPTIONS about the ids here, probably bad but whatever

                ////Don't want to wait forever for inserts, please use a transaction
                //conn.Insert(users, tsx);
                //conn.Insert(content, tsx);
                //conn.Insert(permissions, tsx);
                //conn.Insert(comments, tsx);

                tsx.Commit();
            }
        }
    }
}