﻿using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using System;
using System.Linq;
using Travis_CI.Api.Data;
using Travis_CI.Seeder;

namespace TravisCI.Tests.Core
{
    public abstract class BaseIntegrationTests
    {      
        protected ApplicationDbContext _context;
        public virtual void SetUp()
        {
            var helper = new Helper();
            var result = helper.GetContextAdnUserManager();
            Console.WriteLine("Deleting databse");
            result.Database.EnsureDeleted();

            _context = result;
           

            Console.WriteLine("Applying Migrations");
            result.Database.Migrate();

            Console.WriteLine("Making sure databse is created ");
            result.Database.EnsureCreated();

            Console.WriteLine("Going to save the data ");


            Data.CreateData(result);
            Console.WriteLine("Adding Data into database");
            result.SaveChanges();

            Console.WriteLine("Database sucessfully seeded");
            var totalTopic = result.People.ToList();
            Console.WriteLine($"Total People seedes is {totalTopic.Count()}");          
        }

        [TearDown]
        public virtual void TearDown()
        {
           
            _context.Database.EnsureDeleted();
        }
    }
}
