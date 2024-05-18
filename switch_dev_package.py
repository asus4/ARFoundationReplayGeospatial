#!/usr/bin/env python3

import argparse
from dataclasses import dataclass
import json
import os

"""
Switch between local and remote packages in the manifest.json file
https://docs.unity3d.com/Manual/upm-manifestPrj.html
"""

@dataclass
class Package:
    package: str
    value: str

    def is_local(self) -> bool:
        return self.value.startswith("file:")

    def validate(self, dependencies: dict[str, str], package_search_path: str):
        if self.package not in dependencies:
            raise ValueError(f'"{self.package}" not found in dependencies')
        if self.is_local():
            package_path = os.path.join(package_search_path, self.value[5:])
            if not os.path.exists(package_path):
                raise ValueError(f'"{package_path}" does not exist')


def replace_package(manifest_file: str, packages: list[Package]):
    """
    Replace the package in the manifest.json file
    """

    # Read manifest.json
    with open(manifest_file, "r") as file:
        manifest = json.load(file)
    if "dependencies" not in manifest:
        raise ValueError("dependencies key not found in manifest.json")
    dependencies = manifest["dependencies"]

    # Replace packages
    package_search_path = os.path.abspath(os.path.dirname(manifest_file))
    for package in packages:
        package.validate(dependencies, package_search_path)
        dependencies[package.package] = package.value

    # Write manifest.json
    with open(manifest_file, "w") as file:
        json.dump(manifest, file, indent=2)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Switch between local/upm package")
    parser.add_argument(
        "mode",
        help="local or remote",
        choices=["local", "remote"],
    )
    parser.add_argument(
        "--manifest",
        default="Packages/manifest.json",
        help="Path to the manifest.json file",
    )
    args = parser.parse_args()

    if args.mode == "local":
        # Switch to local package
        packages = [
            Package(
                "com.github.asus4.arfoundationreplay",
                "file:../../ARFoundationReplay/Packages/com.github.asus4.arfoundationreplay",
            ),
            Package(
                "com.google.ar.core.arfoundation.extensions",
                "file:../../arcore-unity-extensions",
            ),
        ]
    elif args.mode == "remote":
        # Switch to remote package
        packages = [
            Package(
                "com.github.asus4.arfoundationreplay",
                "https://github.com/asus4/ARFoundationReplay.git?path=Packages/com.github.asus4.arfoundationreplay#v0.2.1",
            ),
            Package(
                "com.google.ar.core.arfoundation.extensions",
                "https://github.com/asus4/arcore-unity-extensions.git#v1.43.0-replay",
            ),
        ]

    replace_package(args.manifest, packages)
    print(f"Switched to {args.mode} packages")
