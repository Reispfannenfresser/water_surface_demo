namespace Assets.WaterSurface
{
    [RequireComponent(typeof(MeshFilter))]
    public class FloatingMesh : MonoBehaviour
    {
        public WaterSurface WaterSurface { get; private set; }

        protected MeshFilter MeshFilterComponent { get; private set; }

        protected virtual void Awake()
        {
            if (WaterSurface == null)
            {
                Debug.LogWarning("No WaterSurface assigned. FloatingMesh will not float.");
            }

            MeshFilterComponent = GetComponent<MeshFilter>();
        }
    }
}
