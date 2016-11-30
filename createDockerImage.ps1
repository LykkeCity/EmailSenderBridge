dotnet publish -c Release -o bin\docker;
cd bin\docker;
docker build -t lykkex/emailsenderbroker .
cd ..\..;
