namespace Appy.GitDb.Core.Model
{
    public class Author
    {
        public Author(string name, string email)
        {
            Name = name;
            Email = email;
        }

        public string Name { get; }
        public string Email { get; }
    }
}