namespace Tsg.RdcTester.Model;

public class TestAccount
{
    public string AccountNumber { get; set; }
    public string AccountSuffix { get; set; }

    public static List<TestAccount> GetTestAccounts()
    {
        List<TestAccount> testAccounts = new List<TestAccount>();
        testAccounts.Add(new TestAccount { AccountNumber = "563808", AccountSuffix = "S0001" });
        testAccounts.Add(new TestAccount { AccountNumber = "660976", AccountSuffix = "S0001" });
        testAccounts.Add(new TestAccount { AccountNumber = "677307", AccountSuffix = "S0001" });
        testAccounts.Add(new TestAccount { AccountNumber = "682997", AccountSuffix = "S0001" });
        return testAccounts;
    }
}