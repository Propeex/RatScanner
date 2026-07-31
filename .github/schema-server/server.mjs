import http from 'node:http';
import { buildSchema, graphql } from 'graphql';
import schemaSource from './schema-static.mjs';

const host = '127.0.0.1';
const port = 43117;
const schema = buildSchema(schemaSource);

function readRequestBody(request) {
  return new Promise((resolve, reject) => {
    let body = '';

    request.setEncoding('utf8');
    request.on('data', chunk => {
      body += chunk;
      if (body.length > 10 * 1024 * 1024) {
        reject(new Error('GraphQL request body is too large.'));
        request.destroy();
      }
    });
    request.on('end', () => resolve(body));
    request.on('error', reject);
  });
}

const server = http.createServer(async (request, response) => {
  try {
    if (request.method === 'GET' && request.url === '/health') {
      response.writeHead(200, { 'Content-Type': 'text/plain; charset=utf-8' });
      response.end('ok');
      return;
    }

    if (request.method !== 'POST') {
      response.writeHead(405, { Allow: 'POST' });
      response.end();
      return;
    }

    const rawBody = await readRequestBody(request);
    const payload = JSON.parse(rawBody || '{}');
    const result = await graphql({
      schema,
      source: payload.query ?? '',
      variableValues: payload.variables,
      operationName: payload.operationName,
    });

    response.writeHead(200, { 'Content-Type': 'application/json; charset=utf-8' });
    response.end(JSON.stringify(result));
  } catch (error) {
    response.writeHead(500, { 'Content-Type': 'application/json; charset=utf-8' });
    response.end(JSON.stringify({ errors: [error instanceof Error ? error.message : String(error)] }));
  }
});

server.listen(port, host, () => {
  console.log(`Local Tarkov.dev schema server listening on http://${host}:${port}/graphql`);
});
