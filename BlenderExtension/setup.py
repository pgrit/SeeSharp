from setuptools import setup

setup(
    name='seesharp_binaries',
    version='1.0.0',
    author='Pascal Grittmann',
    url='https://github.com/pgrit/SeeSharp',

    description='SeeSharp binaries for use with the Blender plugin',
    long_description='SeeSharp binaries for use with the Blender plugin',
    long_description_content_type="text/markdown",

    license="MIT",
    packages=['seesharp_binaries'],
    package_dir={'seesharp_binaries': 'seesharp_binaries'},
    classifiers=[
        "Programming Language :: Python :: 3",
        "Operating System :: OS Independent",
    ],
    python_requires='>=3.6',
    install_requires=[
    ],
    include_package_data=True,
)
