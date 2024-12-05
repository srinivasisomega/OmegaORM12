using System;
using System.Collections.Generic;
using OmegaORM;

    [Table("Users")]
    public class User
    {
        [PrimaryKey(IsIdentity = false)]
        [Column("Id", IsNullable = false, Length = 50)]
        public string Id { get; set; }

        [Column("Username", IsNullable = false, Length = 50)]
        [Unique]
        public string Username { get; set; }

        [Column("Email", IsNullable = false, Length = 100)]
        public string Email { get; set; }

        [Column("CreatedDate", IsNullable = false)]
        [DefaultValue("GETDATE()")]
        public DateTime CreatedDate { get; set; }

        [Column("IsActive", IsNullable = false)]
        [DefaultValue(true)]
        public bool IsActive { get; set; }

        [OneToOne("UserProfile")]
        public UserProfile Profile { get; set; }

        [OneToMany("UserId")]
        public List<Order> Orders { get; set; } // A user can have multiple orders
    }

    [Table("UserProfiles")]
    public class UserProfile
    {
        [PrimaryKey(IsIdentity = true)]
        [Column("Id", IsNullable = false)]
        public int Id { get; set; }

        [Column("UserId", IsNullable = false)]
        [ForeignKey("Users", "Id")]
        public int UserId { get; set; }

        [Column("FirstName", IsNullable = false, Length = 50)]
        public string FirstName { get; set; }

        [Column("LastName", IsNullable = false, Length = 50)]
        public string LastName { get; set; }

        [Column("DateOfBirth", IsNullable = true)]
        public DateTime? DateOfBirth { get; set; }

        [OneToOne("UserId")]
        public User User { get; set; }
    }

    [Table("Orders")]
    public class Order
    {
        [PrimaryKey(IsIdentity = true)]
        [Column("Id", IsNullable = false)]
        public int Id { get; set; }

        [Column("OrderDate", IsNullable = false)]
        [DefaultValue("GETDATE()")]
        public DateTime OrderDate { get; set; }

        [Column("Amount", IsNullable = false)]
        public decimal Amount { get; set; }

        [Column("UserId", IsNullable = false)]
        [ForeignKey("Users", "Id")]
        public int UserId { get; set; }

       
    }


class Program
{
    static void Main(string[] args)
    {
        string connectionString = "Server=COGNINE-L105;Database=bb2;Trusted_Connection=True;Trust Server Certificate=True;";

        // Create a context instance
        var context = new OmegaContext(connectionString);

        // Synchronize database schema
        context.SynchronizeDatabase(new List<Type> { typeof(User), typeof(UserProfile), typeof(Order) });

        // Create a new user
        //var newUser = new User
        //{
        //    Username = "johndoe",
        //    IsActive = true
        //};
        //context.Save(newUser);

        //// Query users
        //var users = context.Query<User>();
        //foreach (var user in users)
        //{
        //    Console.WriteLine($"User: {user.Username}, IsActive: {user.IsActive}");
        //}
    }
}
