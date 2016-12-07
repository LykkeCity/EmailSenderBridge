dotnet publish -c Release -o bin\docker;
cd bin\docker;
docker rmi -f lykkex/emailsenderbroker
docker build -t lykkex/emailsenderbroker .
cd ..\..;
