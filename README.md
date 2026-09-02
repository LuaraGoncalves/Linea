# Agenda Online

Agenda local com backend em ASP.NET Core e frontend em Vue.js.

## Como rodar

Em um terminal, suba a API:

```powershell
dotnet run --project Agenda.API\Agenda.API.csproj --urls http://localhost:5047
```

Em outro terminal, suba o frontend:

```powershell
cd Agenda.Web
npm install
npm run dev
```

Abra:

```text
http://127.0.0.1:5173/
```

## Login de teste

Quando a API roda em modo de desenvolvimento, ela cria automaticamente este usuario de teste:

```text
E-mail: teste@agenda.local
Senha: 123456
```

Os usuários e as anotações ficam salvos localmente no arquivo:

```text
Agenda.API\App_Data\agenda-data.json
```

Esse arquivo fica no `.gitignore`, porque pode guardar dados pessoais de quem usa a agenda.

## JWT

A API usa JWT assinado para proteger as rotas da agenda. Em desenvolvimento, a chave é temporária e criada quando a API liga. Em produção, configure uma variável de ambiente:

```powershell
$env:AGENDA_JWT_SECRET="troque-por-uma-chave-grande-e-segura-com-mais-de-32-caracteres"
```
