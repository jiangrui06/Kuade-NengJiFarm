namespace WebApplication1.Models
{
    public class ApiResponse<T>
    {
        public int Code { get; set; }
        public string Message { get; set; } = "ok";
        public T? Data { get; set; }

        public static ApiResponse<T> Ok(T? data = default) => new ApiResponse<T> { Code = 0, Message = "ok", Data = data };
        public static ApiResponse<T> Fail(string message, int code = -1) => new ApiResponse<T> { Code = code, Message = message, Data = default };
    }
}
