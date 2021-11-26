using System;
using System.Collections.Generic;
using System.Linq;
using Dapper.Contrib.Extensions;
using Microsoft.Extensions.Logging;

namespace contentapi.test;

public class DbUnitTestFixture : DbUnitTestBase, IDisposable
{
    public DbUnitTestFixture()
    {
        logger.LogDebug($"Adding users to database {MasterConnectionString}");

        //Create tables for the in-memory database we're testing against
        //for this particular test. Note that ALL tests that use this 
        //fixture will get the SAME database!
        using(var conn = CreateNewConnection())
        {
            using(var tsx = conn.BeginTransaction())
            {
                const int basicUser = 1;
                const int adminUser = 2;
                const int carlUser = 3;
                const int basicPage = 1;
                const int adminPage = 2;

                var users = new List<Db.User> {
                    new Db.User() {
                        username = "firstUser",
                        password = "shouldNotBeSearchable",
                        salt = "alsoShouldNotBeSearchable",
                        special = "",
                        createDate = DateTime.Now.AddDays(-10),
                        avatar = 99,
                        email = "secrets@email.com"
                    },
                    new Db.User() {
                        username = "admin",
                        password = "shouldNotBeSearchable",
                        salt = "alsoShouldNotBeSearchable",
                        special = "cutenickname",
                        createDate = DateTime.Now.AddDays(-15),
                        avatar = 1,
                        super = true,
                        email = "admin@email.com"
                    },
                    new Db.User() {
                        username = "carl",
                        password = "differentpassword",
                        salt = "verysalty",
                        createDate = DateTime.Now,
                        avatar = 0,
                        email = "carl@karl.com"
                    },
                };

                var content = new List<Db.Content> {
                    new Db.Content() {
                        name = "A test page",
                        createDate = DateTime.Now.AddDays(-9),
                        publicType = "test",
                        internalType = Db.InternalContentType.page,
                        createUserId = basicUser,
                        content = "A long time ago, someone tried to test"
                    },
                    new Db.Content() {
                        name = "Private room",
                        createDate = DateTime.Now.AddDays(-19),
                        publicType = "secrets",
                        internalType = Db.InternalContentType.page,
                        createUserId = adminUser,
                        content = "The whole operation is run by Tony Lazuto"
                    },
                    new Db.Content() {
                        createDate = DateTime.Now.AddDays(-5),
                        internalType = Db.InternalContentType.file,
                        createUserId = basicUser,
                        publicType = "mybucket",
                        content = "img/jpeg"
                    },
                    new Db.Content() {
                        name = "carl's special pic",
                        createDate = DateTime.Now.AddDays(-7),
                        internalType = Db.InternalContentType.file,
                        createUserId = carlUser,
                        content = "img/png"
                    }
                };

                var permissions = new List<Db.ContentPermission> {
                    new Db.ContentPermission()
                    {
                        contentId = adminPage,
                        userId = adminUser,
                        create = true, update = true, read = true, delete = true
                    },
                    new Db.ContentPermission()
                    {
                        contentId = adminPage,
                        userId = carlUser,
                        create = true, read = true
                    }
                };

                var comments = new List<Db.Comment> {
                    new Db.Comment() {
                        createUserId = carlUser,
                        contentId = adminPage,
                        createDate = DateTime.Now.AddDays(-1),
                        text = "hi i'm paul"
                    },
                    new Db.Comment() {
                        createUserId = adminUser,
                        contentId = basicPage,
                        createDate = DateTime.Now,
                        text = "yes I agree"
                    }
                };

                //We're making ASSUMPTIONS about the ids here, probably bad but whatever

                //Don't want to wait forever for inserts, please use a transaction
                conn.Insert(users, tsx);
                conn.Insert(content, tsx);
                conn.Insert(permissions, tsx);
                conn.Insert(comments, tsx);

                tsx.Commit();
            }
        }
    }
}