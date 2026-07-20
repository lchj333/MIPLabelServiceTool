namespace MIPLabelServiceTool.Models
{
    public class DecResult
    {
        public DecResult(string processName, bool returnResult, string errorMsg)
        {
            process_name = processName;
            return_result = returnResult;
            error_msg = errorMsg;
        }
        public DecResult() { }
        
        public string? process_name { get; set; }
        public bool return_result { get; set; }
        public string? error_msg { get; set; }
    }
}
