namespace WebAPI.Dtos
{
    public class ApiResponses<T>
    {
        public int Code { get; set; } = 200;
        public string Message { get; set; } = "����ɹ�";
        public T? Data { get; set; }

        public static ApiResponses<T> Success(T? data = default, string message = "����ɹ�")
        {
            return new ApiResponses<T> { Code = 200, Message = message, Data = data };
        }

        public static ApiResponses<T> Error(int code, string message)
        {
            return new ApiResponses<T> { Code = code, Message = message };
        }
    }

    public class ApiResponse
    {
        public int Code { get; set; } = 200;
        public string Message { get; set; } = "����ɹ�";
        public object? Data { get; set; }
    }
}