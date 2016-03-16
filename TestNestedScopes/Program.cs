using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using System.Transactions;
using Dapper;

namespace TestNestedScopes
{
  class Program
  {
    private static readonly string Cs = ConfigurationManager.ConnectionStrings["Sample"].ConnectionString;

    static void Main(string[] args)
    {
      // Outer scope does not complete, inner scope does: should have no new users
      NestedScopes("NoUsersAdded", false, true);

      // Both scopes call .Complete: should have new users
      NestedScopes("UsersAdded", true, true);

      // Outer scope completes, inner scope doesn't: should have new users
      NestedScopes("UsersAdded", true, false);
    }

    /// <summary>
    /// Nested scopes
    /// </summary>
    /// <param name="callerMethod">Caller method</param>
    /// <param name="outerScopeComplete">Request commit for the outer scope</param>
    /// <param name="innerScopeComplete">Rquest commit for the inner scope</param>
    private static void NestedScopes(string callerMethod, bool outerScopeComplete, bool innerScopeComplete)
    {
      Console.WriteLine("{0}, out complete {1}, inner complete {2}", callerMethod, outerScopeComplete, innerScopeComplete);
      Console.WriteLine("\t# of users before: {0}", CountUsers());
      using (var cn = new SqlConnection(Cs))
      {
        using (var scope = new TransactionScope())
        {
          cn.Execute("insert into users (name) values (@name)", new {
            name = "outer " + Guid.NewGuid()
          });

          AddAnother(cn, innerScopeComplete);
          
          // commit?
          // TransactionAbortedException if the inner scope has not completed
          if (outerScopeComplete && innerScopeComplete)
            scope.Complete();
        }

        Console.WriteLine("\t# of users after: {0}", CountUsers());
      }
      
    }

    private static int CountUsers()
    {
      using (var cn = new SqlConnection(Cs))
      {
        return cn.ExecuteScalar<int>("select count(*) from users");
      }
    }

    private static void AddAnother(IDbConnection cn, bool commit = true)
    {
      using (var scope = new TransactionScope())
      {
        cn.Execute("insert into users (name) values (@name)", new {
          name = "inner " + Guid.NewGuid()
        });

        // DO COMMIT
        if (commit)
          scope.Complete();
      }
    }
  }
}
