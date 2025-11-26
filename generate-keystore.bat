@echo off
echo 正在生成密钥库文件...
keytool -genkeypair -v -keystore my-release-key.jks -keyalg RSA -keysize 2048 -validity 10000 -alias my-key-alias -storepass android -keypass android -dname "CN=Test, OU=Test, O=Test, L=Test, ST=Test, C=US"
echo.
echo 密钥库已生成：my-release-key.jks
echo 密钥库密码：android
echo 密钥别名：my-key-alias
echo 密钥密码：android
pause
