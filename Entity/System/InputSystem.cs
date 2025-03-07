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

    public Vector2 AimVector;
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
        AimVector = Vector2.Zero,
        MousePosition = Vector2.Zero,
    };

    public InputSystem(World world)
    {
        World = world;
    }

    public void Update(GameTime gameTime)
    {
        UpdateKeyboard();
        UpdateMouse();
        UpdateController();

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

    private void UpdateKeyboard()
    {
        var keyboard = Keyboard.GetState();

        State.Up = keyboard.IsKeyDown(Keys.W) ? 1 : 0;
        State.Down = keyboard.IsKeyDown(Keys.S) ? 1 : 0;
        State.Left = keyboard.IsKeyDown(Keys.A) ? 1 : 0;
        State.Right = keyboard.IsKeyDown(Keys.D) ? 1 : 0;
        State.Jump = keyboard.IsKeyDown(Keys.Space);
    }

    private void UpdateMouse()
    {
        var mouse = Mouse.GetState();

        State.MousePosition = new Vector2(mouse.X, mouse.Y);
        State.Shoot = mouse.LeftButton == ButtonState.Pressed;
    }

    private void UpdateController()
    {
        var controller = GamePad.GetState(PlayerIndex.One);

        State.AimVector = controller.ThumbSticks.Right;
        State.Shoot = controller.Triggers.Right > 0.5f;

        State.Up = controller.ThumbSticks.Left.Y;
        State.Down = -controller.ThumbSticks.Left.Y;
        State.Left = -controller.ThumbSticks.Left.X;
        State.Right = controller.ThumbSticks.Left.X;
    }

    public InputState GetState()
    {
        return State;
    }
}
