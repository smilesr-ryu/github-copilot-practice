using CustomerManager.Models;

namespace CustomerManager.Services;

public interface ICustomerService
{
    Customer? GetCustomer(int id);
    Customer? SearchCustomer(string name);
    List<Customer> GetAllCustomers();
    Customer CreateCustomer(Customer customer);
    Customer? UpdateCustomer(int id, Customer customer);
    bool DeleteCustomer(int id);
}

public class CustomerService : ICustomerService
{
    private static readonly List<Customer> Customers = new()
    {
        new Customer { Id = 1, Name = "John Doe", Email = "john@example.com", CreatedAt = DateTime.Now },
        new Customer { Id = 2, Name = "Jane Smith", Email = "jane@example.com", CreatedAt = DateTime.Now },
        new Customer { Id = 3, Name = "Bob Wilson", Email = "bob@example.com", CreatedAt = DateTime.Now }
    };

    public Customer? GetCustomer(int id)
    {
        return Customers.FirstOrDefault(c => c.Id == id);
    }

    public Customer? SearchCustomer(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return Customers.FirstOrDefault(c => 
            c.Name != null && c.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    }

    public List<Customer> GetAllCustomers()
    {
        return Customers;
    }

    public Customer CreateCustomer(Customer customer)
    {
        var nextId = Customers.Count == 0 ? 1 : Customers.Max(c => c.Id) + 1;
        var newCustomer = new Customer
        {
            Id = nextId,
            Name = customer.Name,
            Email = customer.Email,
            CreatedAt = DateTime.UtcNow
        };

        Customers.Add(newCustomer);
        return newCustomer;
    }

    public Customer? UpdateCustomer(int id, Customer customer)
    {
        var existing = Customers.FirstOrDefault(c => c.Id == id);
        if (existing == null)
        {
            return null;
        }

        existing.Name = customer.Name;
        existing.Email = customer.Email;
        return existing;
    }

    public bool DeleteCustomer(int id)
    {
        var customer = Customers.FirstOrDefault(c => c.Id == id);
        if (customer == null)
        {
            return false;
        }

        Customers.Remove(customer);
        return true;
    }
}
