# LG webOS Remote 📺📱

<p align="center">
  <img src="Resources/drawable/app_logo.png" width="96" height="96" alt="LG webOS Remote Logo" />
</p>

<p align="center">
  <b>Um controle remoto moderno, rápido e intuitivo para Smart TVs LG webOS no Android.</b>
</p>

<p align="center">
  <a href="README.md">🇧🇷 Português</a> •
  <a href="README_EN.md">🇺🇸 English</a> •
  <a href="README_ES.md">🇪🇸 Español</a>
</p>

---

## 🌟 Visão Geral

O **LG webOS Remote** é um aplicativo Android desenvolvido em **.NET 10** para permitir o controle total da sua Smart TV LG webOS diretamente pelo seu smartphone, utilizando a sua rede Wi-Fi local.

Funciona em qualquer casa e em qualquer roteador: basta que a Smart TV e o smartphone estejam conectados na mesma rede local. Não requer servidores externos e não envia seus dados para a nuvem.

---

## 📸 Telas do Aplicativo

<div align="center">
  <table>
    <tr>
      <td align="center" width="33%">
        <b>Controle Remoto</b><br/>
        <img src="screenshots/remote.jpg" width="220" alt="Tela de Controle Remoto" />
      </td>
      <td align="center" width="33%">
        <b>Áudio</b><br/>
        <img src="screenshots/audio.jpg" width="220" alt="Tela de Áudio" />
      </td>
      <td align="center" width="33%">
        <b>Aplicativos</b><br/>
        <img src="screenshots/apps.jpg" width="220" alt="Tela de Aplicativos" />
      </td>
    </tr>
    <tr>
      <td align="center" width="33%">
        <b>Entradas (HDMI)</b><br/>
        <img src="screenshots/inputs.jpg" width="220" alt="Tela de Entradas" />
      </td>
      <td align="center" width="33%">
        <b>Ajustes de Imagem</b><br/>
        <img src="screenshots/picture.jpg" width="220" alt="Tela de Ajustes de Imagem" />
      </td>
      <td align="center" width="33%">
        <b>Ajustes e Conexão</b><br/>
        <img src="screenshots/settings.jpg" width="220" alt="Tela de Ajustes e Conexão" />
      </td>
    </tr>
  </table>
</div>

---

## ✨ Funcionalidades

- 🕹️ **Controle Remoto Completo**: D-Pad circular ergonômico, botão OK central, Home, Voltar, Menu, Sair, Volume (+/-), Canais (+/-) e Mudo.
- 🚀 **Atalhos Rápidos de Streaming**: Acesse instantaneamente **Netflix**, **YouTube**, **HBO Max** e **Prime Video** com um único toque.
- 🔌 **Gerenciamento de Energia**: Desligue a TV pelo aplicativo quando conectada, ou reconecte com um clique.
- 🔊 **Controle Fino de Áudio**: Slider deslizante de volume de 0 a 100, indicador numérico e botão de Mudo / Ativar Som.
- 📱 **Catálogo de Apps em Ordem Alfabética**: Todos os aplicativos instalados na sua TV listados de A a Z com barra de pesquisa em tempo real.
- 📺 **Comutador de Entradas**: Alterne rapidamente entre HDMI 1, HDMI 2, HDMI 3, AV e Componente.
- 🎨 **Calibração de Imagem**: Ajuste Luz de Fundo (Backlight), Brilho, Contraste e Cor da TV em tempo real.
- 🔍 **Descoberta Automática de TVs**: Varre a rede Wi-Fi via protocolo SSDP para encontrar TVs LG automaticamente.
- ✍️ **Configuração Manual**: Permite informar manualmente o IPv4 da TV caso a rede bloqueie transmissões multicast.
- 🌐 **Suporte Multi-idioma**: Alterne em tempo real entre **Português**, **Inglês** e **Espanhol** direto na aba de Ajustes.
- 🔒 **Conexão Segura e Persistente**: Pareamento oficial via WebSocket com armazenamento seguro da chave de autorização no dispositivo.

---

## 🚀 Como Conectar à sua TV

### 1. Pré-requisitos
1. Certifique-se de que a sua **Smart TV LG** está ligada.
2. Certifique-se de que a TV e o seu celular Android estão conectados na **mesma rede Wi-Fi**.

### 2. Primeiro Acesso e Pareamento
1. Abra o aplicativo **LG webOS Remote**.
2. Vá até a aba **Ajustes** (ícone de engrenagem no menu inferior).
3. Toque no botão **Buscar TVs LG na Rede Wi-Fi**:
   - A lista exibirá as TVs encontradas na sua casa. Toque sobre a sua TV para conectar.
   - *Alternativa*: Se o roteador bloquear a busca automática, digite o endereço IPv4 da sua TV no campo **IPv4 da TV** (ex: `192.168.1.100`) e toque em **Salvar e Conectar**.
4. **Olhe para a tela da sua TV LG**:
   - Uma mensagem de solicitação de pareamento aparecerá na TV: *"LG webOS Remote solicita conexão"*.
   - Usando o controle físico da TV, selecione **Permitir** / **Sim**.
5. O aplicativo exibirá um popup de confirmação: **"Conectado com Sucesso! 🎉"**.
6. Pronto! A chave foi salva no celular e agora você pode usar o controle livremente.

---

## 🌐 Como Mudar o Idioma

1. Abra o aplicativo e acesse a aba **Ajustes**.
2. Na seção **Idioma do Aplicativo (Language)**, toque em:
   - 🇧🇷 **Português**
   - 🇺🇸 **English**
   - 🇪🇸 **Español**
3. O aplicativo recarrega instantaneamente na língua escolhida e salva sua preferência.

---

## 📥 Como Instalar (.APK)

1. Vá até a aba [Releases](../../releases) deste repositório no GitHub.
2. Baixe o arquivo `LGwebOSRemote-Release.apk`.
3. No seu Android, abra o arquivo baixado e permita a instalação de aplicativos de fontes desconhecidas se solicitado.
4. Conclua a instalação e abra o aplicativo!

---

## 🛠️ Tecnologias Utilizadas

- **C# / .NET 10 Android**
- **Material Components for Android** (BottomNavigationView, Toolbar, Cards)
- **WebSockets (`ClientWebSocket`)** para comunicação bidirecional com o webOS
- **SSDP / UDP Sockets** para descoberta automática na LAN
- **JSON System.Text.Json**

---

## 📄 Licença

Distribuído sob a licença MIT. Consulte o arquivo `LICENSE` para mais detalhes.