namespace Benchmark
{
    public class Person
    {
        public Person()
        {
            _buffer = new byte[32];
        }

        public Person(Person person) : this()
        {
            Id = person?.Id ?? 0;
        }

        private byte[] _buffer;
        private string _field0;
        private string _field1;
        private string _field2;
        private string _field3;
        private string _field4;

        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int Age { get; set; }
    }
}
