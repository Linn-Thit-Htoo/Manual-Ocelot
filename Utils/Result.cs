namespace Manual_Ocelot.Utils
{
    public class Result<T>
    {
        public HttpStatusCode StatusCode { get; set; }
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = null!;
        public T? Data { get; set; }

        public static Result<T> Success(string message = "Success")
        {
            return new Result<T>
            {
                Message = message,
                IsSuccess = true,
                StatusCode = HttpStatusCode.OK,
            };
        }

        public static Result<T> Success(T data, string message = "Success")
        {
            return new Result<T>
            {
                Data = data,
                IsSuccess = true,
                Message = message,
                StatusCode = HttpStatusCode.OK,
            };
        }

        public static Result<T> Fail(
            string message = "Fail.",
            HttpStatusCode statusCode = HttpStatusCode.BadRequest
        )
        {
            return new Result<T>
            {
                IsSuccess = false,
                Message = message,
                StatusCode = statusCode,
            };
        }

        public static Result<T> Fail(Exception ex)
        {
            return new Result<T>
            {
                IsSuccess = false,
                Message = ex.ToString(),
                StatusCode = HttpStatusCode.InternalServerError,
            };
        }

        public static Result<T> NotFound(string message = "No data found.")
        {
            return Result<T>.Fail(message, HttpStatusCode.NotFound);
        }

        public static Result<T> Duplicate(string message = "Duplicate data.")
        {
            return new Result<T>
            {
                IsSuccess = false,
                Message = message,
                StatusCode = HttpStatusCode.Conflict,
            };
        }
    }
}
