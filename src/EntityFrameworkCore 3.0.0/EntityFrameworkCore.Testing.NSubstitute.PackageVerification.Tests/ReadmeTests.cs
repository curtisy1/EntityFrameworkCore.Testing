using System;
using System.Collections.Generic;
using System.Linq;
using AutoFixture;
using EntityFrameworkCore.Testing.Common.Tests;
using EntityFrameworkCore.Testing.NSubstitute.Extensions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NUnit.Framework;

namespace EntityFrameworkCore.Testing.NSubstitute.PackageVerification.Tests
{
    public class ReadmeTests
    {
        public Fixture Fixture = new Fixture();

        [SetUp]
        public virtual void SetUp()
        {
            //LoggerHelper.LoggerFactory.AddConsole(LogLevel.Debug);
        }

        [Test]
        public void SetAddAndPersist_Item_Persists()
        {
            var mockedDbContext = Create.MockedDbContextFor<TestDbContext>();

            var testEntity = Fixture.Create<TestEntity>();

            mockedDbContext.Set<TestEntity>().Add(testEntity);
            mockedDbContext.SaveChanges();

            Assert.Multiple(() =>
            {
                Assert.AreNotEqual(default(Guid), testEntity.Guid);
                Assert.DoesNotThrow(() => mockedDbContext.Set<TestEntity>().Single());
                Assert.AreEqual(testEntity, mockedDbContext.Find<TestEntity>(testEntity.Guid));
            });
        }

        [Test]
        public void FromSqlRaw_AnyStoredProcedureWithNoParameters_ReturnsExpectedResult()
        {
            var mockedDbContext = Create.MockedDbContextFor<TestDbContext>();

            var expectedResult = Fixture.CreateMany<TestEntity>().ToList();

            mockedDbContext.Set<TestEntity>().AddFromSqlRawResult(expectedResult);

            var actualResult = mockedDbContext.Set<TestEntity>().FromSqlRaw("sp_NoParams").ToList();

            Assert.Multiple(() =>
            {
                Assert.IsNotNull(actualResult);
                Assert.IsTrue(actualResult.Any());
                CollectionAssert.AreEquivalent(expectedResult, actualResult);
            });
        }

        [Test]
        public void FromSqlRaw_SpecifiedStoredProcedureAndParameters_ReturnsExpectedResult()
        {
            var mockedDbContext = Create.MockedDbContextFor<TestDbContext>();

            var sqlParameters = new List<SqlParameter> {new SqlParameter("@SomeParameter2", "Value2")};
            var expectedResult = Fixture.CreateMany<TestEntity>().ToList();

            mockedDbContext.Set<TestEntity>().AddFromSqlRawResult("sp_Specified", sqlParameters, expectedResult);

            var actualResult = mockedDbContext.Set<TestEntity>().FromSqlRaw("[dbo].[sp_Specified] @SomeParameter1 @SomeParameter2", new SqlParameter("@someparameter2", "Value2")).ToList();

            Assert.Multiple(() =>
            {
                Assert.IsNotNull(actualResult);
                Assert.IsTrue(actualResult.Any());
                CollectionAssert.AreEquivalent(expectedResult, actualResult);
            });
        }

        [Test]
        public void QueryAddRange_Enumeration_AddsToQuerySource()
        {
            var mockedDbContext = Create.MockedDbContextFor<TestDbContext>();

            var expectedResult = Fixture.CreateMany<TestQuery>().ToList();

            mockedDbContext.Query<TestQuery>().AddRangeToReadOnlySource(expectedResult);

            Assert.Multiple(() =>
            {
                CollectionAssert.AreEquivalent(expectedResult, mockedDbContext.Query<TestQuery>().ToList());
                CollectionAssert.AreEquivalent(mockedDbContext.Query<TestQuery>().ToList(), mockedDbContext.TestView.ToList());
            });
        }

        [Test]
        public void ExecuteSqlCommand_SpecifiedStoredProcedure_ReturnsExpectedResult()
        {
            var mockedDbContext = Create.MockedDbContextFor<TestDbContext>();

            var commandText = "sp_NoParams";
            var expectedResult = 1;

            mockedDbContext.AddExecuteSqlCommandResult(commandText, new List<SqlParameter>(), expectedResult);

            var result = mockedDbContext.Database.ExecuteSqlCommand("sp_NoParams");

            Assert.AreEqual(expectedResult, result);
        }

        [Test]
        public void ExecuteSqlCommand_SpecifiedStoredProcedureAndSqlParameters_ReturnsExpectedResult()
        {
            var mockedDbContext = Create.MockedDbContextFor<TestDbContext>();

            var commandText = "sp_WithParams";
            var sqlParameters = new List<SqlParameter> {new SqlParameter("@SomeParameter2", "Value2")};
            var expectedResult = 1;

            mockedDbContext.AddExecuteSqlCommandResult(commandText, sqlParameters, expectedResult);

            var result = mockedDbContext.Database.ExecuteSqlCommand("[dbo].[sp_WithParams] @SomeParameter2", sqlParameters);

            Assert.AreEqual(expectedResult, result);
        }

        [Test]
        public void AddRangeThenSaveChanges_CanAssertInvocationCount()
        {
            var mockedDbContext = Create.MockedDbContextFor<TestDbContext>();

            mockedDbContext.Set<TestEntity>().AddRange(Fixture.CreateMany<TestEntity>().ToList());
            mockedDbContext.SaveChanges();

            Assert.Multiple(() =>
            {
                mockedDbContext.Received(1).SaveChanges();

                //The db set is a mock, so we need to ensure we invoke the verify on the db set mock
                mockedDbContext.Set<TestEntity>().Received(1).AddRange(Arg.Any<IEnumerable<TestEntity>>());

                //This is the same mock instance as above, just accessed a different way
                mockedDbContext.TestEntities.Received(1).AddRange(Arg.Any<IEnumerable<TestEntity>>());

                Assert.That(mockedDbContext.TestEntities, Is.SameAs(mockedDbContext.Set<TestEntity>()));
            });
        }
    }
}