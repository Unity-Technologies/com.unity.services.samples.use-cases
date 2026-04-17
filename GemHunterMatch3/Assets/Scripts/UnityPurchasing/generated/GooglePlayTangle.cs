// WARNING: Do not modify! Generated file.

namespace UnityEngine.Purchasing.Security {
    public class GooglePlayTangle
    {
        private static byte[] data = System.Convert.FromBase64String("qepfE8x4IaqNGeOxlrxUGE5R0l82llTJt2qy3R3gwEzTqLyT+iw23A0m8nSqr/glhUfNyoVcAQaJeGbC/H9xfk78f3R8/H9/foVmBPRwcoF4V3A/PUzcVn2ozstxoDXvH/aWHlRW4g99xCpH02gu0DzfNwpGGBAQies9FPSPvZZ/5ASz4kdu0IEc5gNO/H9cTnN4d1T4NviJc39/f3t+faCwWpTQp5MD2CTMtODYR29sN9n5PKhjVdFtqFdQnw9i60CgHwE9zLkX/K8HYdZkDcq4FnUH1OgCB3fM//I6sZbp1oUfmC91oHgWBp44C9+78k/u1dpXCL1DNZl0C6kpp5IEgWTov9akIu3EebGqGygp3XHHV/fVDEDLuoHuorTyK3x9f35/");
        private static int[] order = new int[] { 9,3,2,3,4,12,10,9,12,11,11,13,13,13,14 };
        private static int key = 126;

        public static readonly bool IsPopulated = true;

        public static byte[] Data() {
        	if (IsPopulated == false)
        		return null;
            return Obfuscator.DeObfuscate(data, order, key);
        }
    }
}
