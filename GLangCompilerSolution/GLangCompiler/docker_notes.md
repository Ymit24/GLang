# Notes
Use the following command to run docker which will allow compilation and running of GLang programs on OS's and architectures other than Linux x86

```
docker build --platform linux/amd64 . --tag custom_nasm
docker run --platform linux/amd64 -it --mount src="$(pwd)",target=/test_container,type=bind custom_nasm
```
