FROM mcr.microsoft.com/dotnet/runtime:7.0

WORKDIR /app

COPY . /app

RUN chmod 777 ./HidamariBot.dll

ENTRYPOINT ["dotnet", "HidamariBot.dll"]