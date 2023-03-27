using System.Threading.Channels;

namespace pl_lab_3 {

    class TokenRingNode {
        private static int idCounter = 0;

        public int id { get; }
        public Channel<Token>? channelFrom, channelTo;

        public TokenRingNode() {
            id = idCounter++;
        }

        private async Task WaitToken() {
            for( ; ; ) {
                Token token = await AcceptToken();
                await ProcessToken( token );
            }
        }

        public async Task InitiateRequest( Token token ) {
            await SendToNext( token );
            await WaitToken();
        }
        public async void Wait() {
            await WaitToken();
        }

        private async Task SendToNext( Token token ) {
            if( channelTo == null ) {
                throw new Exception( $"Для узла { id } не установлен выходной канал." );
            }

            await channelTo.Writer.WriteAsync( token );
        }

        private async Task<Token> AcceptToken() {
            if( channelFrom == null ) {
                throw new Exception( $"Для узла { id } не установлен входной канал." );
            }

            await channelFrom.Reader.WaitToReadAsync();
            return await channelFrom.Reader.ReadAsync();
        }

        private async Task ProcessToken( Token token ) {
            if( token.recipient == id ) {
                Console.WriteLine( $"Узел { id } успешно получил сообщение! Текст сообщения: '{ token.data }'" );
            } else {
                token.ttl -= 1;
                Console.WriteLine( $"Маркер находится на узле { id }. Количество оставшихся переходов - { token.ttl }." );

                if( token.ttl > 0 ) {
                    await SendToNext( token );
                } else {
                    Console.WriteLine( "Время жизни маркера истекло." );
                }
            }
        }
    }

    struct Token {
        public string data;
        public int recipient;
        public int ttl;
    }

    internal class Program {
        static async Task Main() {
            // Работа с пользователем
            Console.Write( "Введите количество узлов сети - " );
            if( !int.TryParse( Console.ReadLine(), out int nodeAmount ) ) {
                Console.WriteLine( "Не удалось считать число. Выбрано число по умолчанию." );
                nodeAmount = 10;
            };
            Console.WriteLine( $"Количество узлов - { nodeAmount }.\n" );

            Console.Write( "Введите время жизни маркера - " );
            if( !int.TryParse( Console.ReadLine(), out int tokenTtl ) ) {
                Console.WriteLine( "Не удалось считать число. Выбрано число по умолчанию." );
                tokenTtl = 6;
            };
            Console.WriteLine( $"Время жизни маркера - { tokenTtl }.\n" );

            Console.Write( "Введите идентификатор узла-получаетеля - " );
            if( !int.TryParse( Console.ReadLine(), out int acceptorId ) || acceptorId >= nodeAmount || acceptorId < 0 ) {
                Console.WriteLine( "Не удалось считать число. Выбрано число по умолчанию." );
                acceptorId = nodeAmount - 1;
            };
            Console.WriteLine( $"Идентификатор узла-получателя - { acceptorId }.\n" );

            Console.WriteLine( "Введите сообщение, которое будет передано на узел:" );
            string messageText = Console.ReadLine() ?? "";

            Console.WriteLine( "\nДанные получены, идёт выполнение программы...\n" );

            // Создание узлов сети
            TokenRingNode[] tokenRingNodes = new TokenRingNode[ nodeAmount ];
            for( int i = 0; i < nodeAmount; i++ ) {
                tokenRingNodes[ i ] = new TokenRingNode();
            };

            // Создание каналов между узлами
            for( int i = 0; i < nodeAmount - 1; i++ ) {
                Channel<Token> newChannel = Channel.CreateBounded<Token>( 1 );

                tokenRingNodes[ i + 1 ].channelFrom =
                    tokenRingNodes[ i ].channelTo = newChannel;
            };

            // Создание канала, связывающего конец и начало узлов
            tokenRingNodes[ 0 ].channelFrom = 
                tokenRingNodes[ nodeAmount - 1 ].channelTo =
                    Channel.CreateBounded<Token>( 1 );

            // Инициализируем потоки, оставляем узлы дожидаться маркера
            for( int i = 1; i < nodeAmount - 1; i++ ) {
                Thread newThread = new( tokenRingNodes[ i ].Wait );
                newThread.Start();
            };

            // Узел с идентификатором 0 будет выполняться в основном потоке
            // Выдаём маркер узлу под номером 0, запускаем его передачу
            await tokenRingNodes[0].InitiateRequest( new Token {
                data = messageText,
                recipient = acceptorId,
                ttl = tokenTtl
            });
        }
    }
}
