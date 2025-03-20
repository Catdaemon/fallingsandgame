using System;
using Arch.Core;
using FallingSand.Entity.Component;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace FallingSand.Entity.System;

struct InputState
{
    public float Up;
    public float Down;
    public float Left;
    public float Right;
    public bool Jump;
    public bool Shoot;
    public bool InventoryNext;
    public bool InventoryPrevious;

    public Vector2 AimVector;
    public readonly Vector2 NormalisedMoveVector => new(Right - Left, Down - Up);
    public Vector2 MousePosition;
}

class InputSystem : ISystem
{
    private readonly World World;
    private InputState State = new()
    {
        Up = 0,
        Down = 0,
        Left = 0,
        Right = 0,
        Jump = false,
        Shoot = false,
        InventoryNext = false,
        InventoryPrevious = false,
        AimVector = Vector2.Zero,
        MousePosition = Vector2.Zero,
    };

    public InputSystem(World world)
    {
        World = world;
    }

    public void Update(GameTime gameTime)
    {
        ZeroState();

        State = CombineStates(UpdateKeyboard(), UpdateMouse(), UpdateController());

        // Update the input state component for all entities
        // Only entities with both InputReceiverComponent and InputStateComponent will be updated
        var query = new QueryDescription().WithAll<InputReceiverComponent, InputStateComponent>();
        World.Query(
            in query,
            (
                Arch.Core.Entity entity,
                ref InputReceiverComponent receiver,
                ref InputStateComponent inputState
            ) =>
            {
                inputState.Value = State;
            }
        );
    }

    private void ZeroState()
    {
        State.Up = 0;
        State.Down = 0;
        State.Left = 0;
        State.Right = 0;
        State.Jump = false;
        State.Shoot = false;
        State.AimVector = Vector2.Zero;
        State.MousePosition = Vector2.Zero;
        State.InventoryNext = false;
        State.InventoryPrevious = false;
    }

    private InputState UpdateKeyboard()
    {
        var keyboard = Keyboard.GetState();

        return new InputState
        {
            Up = keyboard.IsKeyDown(Keys.W) ? 1 : 0,
            Down = keyboard.IsKeyDown(Keys.S) ? 1 : 0,
            Left = keyboard.IsKeyDown(Keys.A) ? 1 : 0,
            Right = keyboard.IsKeyDown(Keys.D) ? 1 : 0,
            Jump = keyboard.IsKeyDown(Keys.Space),
        };
    }

    private InputState UpdateMouse()
    {
        var screenSize = Camera.GetSize();
        var mouse = Mouse.GetState();

        // Calculate normal aim vector from the center of the screen
        var center = new Vector2(screenSize.X / 2, screenSize.Y / 2);
        var aimVector = new Vector2(mouse.X, mouse.Y) - center;
        aimVector.Normalize();

        return new InputState
        {
            AimVector = aimVector,
            Shoot = mouse.LeftButton == ButtonState.Pressed,
        };
    }

    private InputState UpdateController()
    {
        var controller = GamePad.GetState(PlayerIndex.One);

        return new InputState
        {
            AimVector = controller.ThumbSticks.Right,
            Shoot = controller.Triggers.Right > 0.5f,

            Up = controller.ThumbSticks.Left.Y,
            Down = -controller.ThumbSticks.Left.Y,
            Left = -controller.ThumbSticks.Left.X,
            Right = controller.ThumbSticks.Left.X,
        };
    }

    private InputState CombineStates(params InputState[] states)
    {
        var combinedState = new InputState();

        foreach (var state in states)
        {
            combinedState.Up = combinedState.Up > 0 ? combinedState.Up : state.Up;
            combinedState.Down = combinedState.Down > 0 ? combinedState.Down : state.Down;
            combinedState.Left = combinedState.Left > 0 ? combinedState.Left : state.Left;
            combinedState.Right = combinedState.Right > 0 ? combinedState.Right : state.Right;
            combinedState.Jump = combinedState.Jump || state.Jump;
            combinedState.Shoot = combinedState.Shoot || state.Shoot;
            combinedState.AimVector =
                combinedState.AimVector != Vector2.Zero ? combinedState.AimVector : state.AimVector;
            combinedState.MousePosition =
                combinedState.MousePosition != Vector2.Zero
                    ? combinedState.MousePosition
                    : state.MousePosition;
        }

        return combinedState;
    }

    public InputState GetState()
    {
        return State;
    }
}
