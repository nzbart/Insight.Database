﻿using System;
using System.Data;
using System.Data.SqlTypes;
using System.Linq;
using Insight.Database;
using NUnit.Framework;

namespace Insight.Tests
{
	public class TableValuedParameterWithClassesTests : BaseTest
	{
		[TearDown]
		public void TearDown()
		{
			DbSerializationRule.ResetRules();
		}

		class InsightTestDataString
		{
			public MyCustomClass String { get; set; }
		}

		class MyCustomClass : IHasStringValue
		{
			public string Value { get; set; }
		}

		[Test]
		public void Given_a_type_which_requires_serialisation_When_using_the_type_in_an_sql_statement_Then_it_does_not_explode()
		{
			DbSerializationRule.Serialize<MyCustomClass>(new MyCustomSerialiser<MyCustomClass>());

			var roundtrippedValue = Connection().ExecuteScalarSql<string>("select @value", new { value = new MyCustomClass { Value = "classes are best" } });

			Assert.That(roundtrippedValue, Is.EqualTo("classes are best"));
		}

		[Test]
		public void Given_a_table_type_with_a_property_that_is_a_class_which_requires_serialisation_When_using_the_type_in_an_sql_statement_Then_it_does_not_explode()
		{
			DbSerializationRule.Serialize<MyCustomClass>(new MyCustomSerialiser<MyCustomClass>());

			var roundtrippedValue = Connection().ExecuteScalarSql<string>("select top 1 String from @values", new { values = new[] { new InsightTestDataString() { String = new MyCustomClass { Value = "classes are better" } } } });

			Assert.That(roundtrippedValue, Is.EqualTo("classes are better"));
		}

		[Test]
		public void Given_a_table_type_with_a_property_that_is_a_class_which_requires_serialisation_When_using_the_type_in_an_sql_statement_in_a_transaction_Then_it_does_not_explode()
		{
			DbSerializationRule.Serialize<MyCustomClass>(new MyCustomSerialiser<MyCustomClass>());

			using (var connection = ConnectionWithTransaction())
			{
				var roundtrippedValue = connection.ExecuteScalarSql<string>("select top 1 String from @values", new {values = new[] {new InsightTestDataString() {String = new MyCustomClass {Value = "classes are better"}}}});

				Assert.That(roundtrippedValue, Is.EqualTo("classes are better"));
			}
		}
	}

	public class TableValuedParameterWithStructsTests : BaseTest
	{
		[TearDown]
		public void TearDown()
		{
			DbSerializationRule.ResetRules();
		}

		struct MyCustomStruct : IHasStringValue
		{
			public string Value { get; set; }
		}

		class InsightTestDataString
		{
			public MyCustomStruct String { get; set; }
		}

		[Test]
		public void Given_a_type_which_requires_serialisation_When_using_the_type_in_an_sql_statement_Then_it_does_not_explode()
		{
			DbSerializationRule.Serialize<MyCustomStruct>(new MyCustomSerialiser<MyCustomStruct>());

			var roundtrippedValue = Connection().ExecuteScalarSql<string>("select @value", new { value = new MyCustomStruct { Value = "structs are best" } });

			Assert.That(roundtrippedValue, Is.EqualTo("structs are best"));
		}

		[Test]
		public void Given_a_table_type_with_a_property_that_is_a_struct_which_requires_serialisation_When_using_the_type_in_an_sql_statement_Then_it_does_not_explode()
		{
			DbSerializationRule.Serialize<MyCustomStruct>(new MyCustomSerialiser<MyCustomStruct>());

			var roundtrippedValue = Connection().ExecuteScalarSql<string>("select top 1 String from @values", new { values = new[] { new InsightTestDataString { String = new MyCustomStruct { Value = "structs are better" } } } });

			Assert.That(roundtrippedValue, Is.EqualTo("structs are better"));
		}
	}

	interface IHasStringValue
	{
		string Value { get; }
	}

	class MyCustomSerialiser<T> : IDbObjectSerializer where T : IHasStringValue
	{
		public bool CanSerialize(Type type, DbType dbType)
		{
			return type == typeof(T);
		}

		public bool CanDeserialize(Type sourceType, Type targetType)
		{
			throw new NotImplementedException();
		}

		public DbType GetSerializedDbType(Type type, DbType dbType)
		{
			return dbType;
		}

		public object SerializeObject(Type type, object value)
		{
			return ((IHasStringValue)value).Value;
		}

		public object DeserializeObject(Type type, object encoded)
		{
			throw new NotImplementedException();
		}
	}

	#region TVP With Date Tests
	[TestFixture]
	public class TvpWithDefaultDateTimeDataTypeIssueTests : BaseTest
	{
		[SetUp]
		public void SetUp()
		{
			Connection().ExecuteSql("create type SimpleDateTable as table (Value date, Value2 datetime not null, Value3 datetime null, Value4 datetime2)");
		}

		[TearDown]
		public void TearDown()
		{
			Connection().ExecuteSql("drop type SimpleDateTable");
		}

		[Test]
		public void TVPs_WithDefaultDateTime_DoesNotBlowUp()
		{
			var sql    = "select count(*) from @values";
			var values = new[]
			{
				WithDate(new DateTime(2020, 3, 17)),
				WithDate(new DateTime()), // default(DateTime)
				WithDate(new DateTime(2020, 3, 19)),
				WithDate(new DateTime(2020, 3, 20))
			};

			var result = Connection().SingleSql<int>(sql, new { values });

			Assert.AreEqual(result, 4);
		}

		private SimpleDate WithDate(DateTime value)
			=> new SimpleDate(value);

		public class SimpleDate
		{
			public SimpleDate(DateTime value)
			{
				Value = value;
				Value2 = default == value ? (DateTime)SqlDateTime.MinValue : value;
				Value3 = default != value ? (DateTime?)value : null;
				Value4 = value;
			}

			public DateTime Value { get; }
			public DateTime Value2 { get; }
			public DateTime? Value3 { get; }
			public DateTime Value4 { get; }
		}
	}
	#endregion

	#region Issue 354 Tests
	[TestFixture]
	public class Issue354Tests : BaseTest
	{
		[SetUp]
		public void SetUp()
		{
			Connection().ExecuteSql("create type SimpleIntTable as table (Value int primary key)");
		}

		[TearDown]
		public void TearDown()
		{
			Connection().ExecuteSql("drop type SimpleIntTable");
		}

		[Test]
		public void TVPsShouldBeCached()
		{
			var sql = "select count(*) from @values";
			var values = Enumerable.Range(1, 4).Select(v => new SimpleInt(v)).ToArray();

			void RunQuery() => Connection().SingleSql<int>(sql, new { values });

			//Run the query twice
			RunQuery();
			RunQuery();
		}

		public class SimpleInt
		{
			public int Value { get; }

			public SimpleInt(int value)
			{
				Value = value;
			}
		}
	}
	#endregion

	#region Issue 448 Tests
	[TestFixture]
	public class Issue448Tests : BaseTest
	{
		[Test]
		public void TestIssue448()
		{
			try
			{
				Connection().ExecuteSql(
					@"CREATE TYPE [ErrorRecord] AS TABLE(
					[Id] [int] NULL,
					[TableId] [int] NULL,
					[DocTypeRowPK] [int] NULL,
					[ErrorJson] varchar(4001) NULL
					)");

				Connection().ExecuteSql(
					@"CREATE PROCEDURE [InsertPdsData] (
					@errors [ErrorRecord] READONLY,
					@someid INT)

					AS BEGIN
						SELECT COUNT(*) FROM @errors
					END");		



				Connection().ExecuteScalar<int>("[InsertPdsData]", new
				{
					Errors = new[] { new { ErrorJson = "test"} },
					SomeId = 1
				});
			}
			finally
			{
				Connection().ExecuteSql("DROP PROC [InsertPdsData]");
				Connection().ExecuteSql("DROP TYPE [ErrorRecord]");
			}
		}
	}
	#endregion

	#region Issue 456 Tests
	[TestFixture]
	public class Issue456Tests : BaseTest
	{

		[Test]
		public void TestIssue456()
		{
			try
			{
				Connection().ExecuteSql(
					@"CREATE TYPE [UniqueIdTable] AS TABLE(
						[ItemId] [uniqueidentifier] NOT NULL
					)");

				Connection().ExecuteSql(
					@"CREATE PROCEDURE [SampleTable_GetOrderedItems]
						@TableEntryIDs [UniqueIdTable] READONLY
					AS BEGIN
						SELECT COUNT(*) FROM @TableEntryIDs
					END");		

				var orderedList = new[] { "a72863cf-c573-4bf8-9a0b-02212f84698a", "56a0c8ef-c826-45a5-bbce-fb334e59f4b7", "26525d03-1a64-4843-bab4-9daf88e9ae02" };
				var result = Connection().ExecuteScalar<int>("SampleTable_GetOrderedItems", new { TableEntryIDs = orderedList });

				Assert.AreEqual(result, 3);
			}
			finally
			{
				Connection().ExecuteSql("DROP PROC [SampleTable_GetOrderedItems]");
				Connection().ExecuteSql("DROP TYPE [UniqueIdTable]");
			}
		}
	}
	#endregion
}
