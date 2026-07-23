namespace PlcSimWebApi
{
    public class WriteRequest
    {
        public string Value { get; set; }
        public string Type { get; set; }
    }

    public class TagItemDto
    {
        public string Name { get; set; }
        public string Type { get; set; }
    }
}