import json
import socket
import threading
from pathlib import Path

CONFIG_PATH = Path(__file__).resolve().parent / "server.config"
DEFAULT_CONFIG = {
    "light_on": False,
    "auto_water_on": False,
    "frequency_hours": 12,
    "dispense_seconds": 10,
    "last_power_action": None,
    "port": 12345,
    "pump_on": False,
}


def load_config() -> dict:
    if not CONFIG_PATH.exists():
        save_config(DEFAULT_CONFIG)
        return dict(DEFAULT_CONFIG)

    with CONFIG_PATH.open("r", encoding="utf-8-sig") as handle:
        return json.load(handle)


def save_config(config: dict) -> None:
    with CONFIG_PATH.open("w", encoding="utf-8") as handle:
        json.dump(config, handle, indent=2, sort_keys=True)
        handle.write("\n")


def send_settings(connection: socket.socket, config: dict) -> None:
    payload = json.dumps(config)
    connection.sendall(f"SETTINGS {payload}\n".encode("utf-8"))


def apply_command(command: str, config: dict) -> None:
    if command == "LIGHT ON":
        config["light_on"] = True
    elif command == "LIGHT OFF":
        config["light_on"] = False
    elif command == "AUTOWATER ON":
        config["auto_water_on"] = True
    elif command == "AUTOWATER OFF":
        config["auto_water_on"] = False
    elif command.startswith("FREQUENCY "):
        value = int(command.split(" ", 1)[1])
        config["frequency_hours"] = max(1, min(48, value))
    elif command.startswith("DISPENSE "):
        value = int(command.split(" ", 1)[1])
        config["dispense_seconds"] = max(1, min(60, value))
    elif command == "PUMP ON":
        config["pump_on"] = True
    elif command == "PUMP OFF":
        config["pump_on"] = False
    elif command == "SHUTDOWN":
        config["last_power_action"] = "shutdown"
    elif command == "RESTART":
        config["last_power_action"] = "restart"


def handle_client(connection: socket.socket, address: tuple[str, int]) -> None:
    print(f"Connected: {address[0]}:{address[1]}")
    config = load_config()
    send_settings(connection, config)

    buffer = ""
    try:
        while True:
            try:
                data = connection.recv(1024)
            except ConnectionResetError:
                break

            if not data:
                break

            buffer += data.decode("utf-8")
            while "\n" in buffer:
                line, buffer = buffer.split("\n", 1)
                command = line.strip()
                if not command:
                    continue

                print(f"Received: {command}")
                apply_command(command, config)
                save_config(config)
                send_settings(connection, config)
    finally:
        connection.close()
        print("Disconnected")


def get_local_ipv4() -> str:
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as probe:
            probe.connect(("8.8.8.8", 80))
            return probe.getsockname()[0]
    except OSError:
        return "unknown"


def run_server(host: str = "0.0.0.0", port: int | None = None) -> None:
    config = load_config()
    server_port = config.get("port", 12345) if port is None else port

    server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    server_socket.bind((host, server_port))
    server_socket.listen(1)

    print(f"Listening on {host}:{server_port}", flush=True)
    print(f"Connect via: IP: \"{get_local_ipv4()}\" Port: \"{server_port}\"", flush=True)
    while True:
        connection, address = server_socket.accept()
        thread = threading.Thread(target=handle_client, args=(connection, address), daemon=True)
        thread.start()


if __name__ == "__main__":
    run_server()
