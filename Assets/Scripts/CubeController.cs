using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class CubeController : MonoBehaviour
    {
        protected Rigidbody RigidbodyComponent { get; private set; } = null;

        [SerializeField]
        private float _jumpForce = 10f;

        [SerializeField]
        private float _movementForce = 10f;

        [SerializeField]
        public Bounds _movementBounds;

        protected virtual void Awake()
        {
            RigidbodyComponent = GetComponent<Rigidbody>();
        }

        public void FixedUpdate()
        {
            StartCoroutine(ClampPosition());
        }

        private IEnumerator ClampPosition()
        {
            yield return new WaitForFixedUpdate();

            Vector3 oldPosition = transform.position;

            transform.position = Vector3.Min(_movementBounds.max, transform.position);
            transform.position = Vector3.Max(_movementBounds.min, transform.position);

            Vector3 velocityResetMask = Vector3.one;

            if (oldPosition.x != transform.position.x)
            {
                velocityResetMask.x = 0;
            }
            if (oldPosition.y != transform.position.y)
            {
                velocityResetMask.y = 0;
            }
            if (oldPosition.z != transform.position.z)
            {
                velocityResetMask.z = 0;
            }

            RigidbodyComponent.linearVelocity = Vector3.Scale(
                RigidbodyComponent.linearVelocity,
                velocityResetMask
            );
        }

        public void Move(InputAction.CallbackContext context)
        {
            Vector2 _movementInput = context.ReadValue<Vector2>();
            Vector3 wordMovement = new(_movementInput.x, 0, _movementInput.y);
            RigidbodyComponent.AddForce(wordMovement * _movementForce, ForceMode.Impulse);
        }

        public void Jump(InputAction.CallbackContext context)
        {
            if (!context.performed)
            {
                return;
            }
            RigidbodyComponent.AddForce(0, _jumpForce, 0, ForceMode.Impulse);
        }

        public void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.orange;
            Gizmos.DrawWireCube(_movementBounds.center, _movementBounds.size);
        }
    }
}
